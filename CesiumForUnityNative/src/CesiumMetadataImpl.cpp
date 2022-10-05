#include "CesiumMetadataImpl.h"

#include <CesiumGltf/AccessorView.h>
#include <CesiumGltf/MetadataFeatureTableView.h>
#include <CesiumGltf/MetadataPropertyView.h>

#include <DotNet/CesiumForUnity/CesiumMetadata.h>
#include <DotNet/UnityEngine/GameObject.h>
#include <DotNet/UnityEngine/Mesh.h>
#include <DotNet/UnityEngine/Transform.h>

using namespace CesiumForUnityNative;
using namespace CesiumGltf;

CesiumMetadataImpl::~CesiumMetadataImpl() {}

void CesiumMetadataImpl::JustBeforeDelete(
        const DotNet::CesiumForUnity::CesiumMetadata& metadata) {}

void CesiumMetadataImpl::loadMetadata() {
    if (this->_pModel && this->_pModelMetadata) {
        for (auto kvp : _pModelMetadata->featureTables) {
            const std::string& featureTableName = kvp.first;
            const CesiumGltf::FeatureTable& featureTable = kvp.second;

            CesiumGltf::MetadataFeatureTableView featureTableView{
                this->_pModel,
                    &featureTable};

            std::unordered_map<std::string, PropertyType>& myFeatureTable =
                this->_featureTables[featureTableName];

            featureTableView.forEachProperty([&myFeatureTable](
                        const std::string& propertyName,
                        auto propertyValue) {
                    std::pair<std::string, PropertyType> p = {propertyName, propertyValue};
                    myFeatureTable.insert(p);
                    });
        }

        this->_pModel->forEachPrimitiveInScene(
                -1,
                [this](
                    const Model& gltf,
                    const Node& node,
                    const Mesh& mesh,
                    const MeshPrimitive& primitive,
                    const glm::dmat4& transform) {
                const ExtensionMeshPrimitiveExtFeatureMetadata* pMetadata =
                primitive
                .getExtension<ExtensionMeshPrimitiveExtFeatureMetadata>();

                AccessorType vertexIDAccessor;

                if (pMetadata) {
                const CesiumGltf::Accessor& indicesAccessor =
                gltf.getSafe(gltf.accessors, primitive.indices);
                switch (indicesAccessor.componentType) {
                case CesiumGltf::Accessor::ComponentType::UNSIGNED_BYTE:
                vertexIDAccessor = CesiumGltf::AccessorView<
                CesiumGltf::AccessorTypes::SCALAR<uint8_t>>(
                        gltf,
                        indicesAccessor);
                break;
                case CesiumGltf::Accessor::ComponentType::UNSIGNED_SHORT:
                vertexIDAccessor = CesiumGltf::AccessorView<
                    CesiumGltf::AccessorTypes::SCALAR<uint16_t>>(
                            gltf,
                            indicesAccessor);
                break;
                case CesiumGltf::Accessor::ComponentType::UNSIGNED_INT:
                vertexIDAccessor = CesiumGltf::AccessorView<
                    CesiumGltf::AccessorTypes::SCALAR<uint32_t>>(
                            gltf,
                            indicesAccessor);
                break;
                default:
                break;
                }

                auto& primitiveInfo = this->_featureIDs.emplace_back(
                        std::pair<
                        AccessorType,
                        std::vector<
                        std::pair<std::string, AccessorType>>>());
                primitiveInfo.first = vertexIDAccessor;
                auto& pairs = primitiveInfo.second;

                for (const CesiumGltf::FeatureIDAttribute& attribute :
                        pMetadata->featureIdAttributes) {
                    if (attribute.featureIds.attribute) {
                        auto featureID = primitive.attributes.find(
                                attribute.featureIds.attribute.value());
                        if (featureID == primitive.attributes.end()) {
                            continue;
                        }

                        const CesiumGltf::Accessor* accessor =
                            gltf.getSafe<CesiumGltf::Accessor>(
                                    &gltf.accessors,
                                    featureID->second);
                        if (!accessor) {
                            continue;
                        }

                        AccessorType featureIDAccessor;
                        switch (accessor->componentType) {
                            case CesiumGltf::Accessor::ComponentType::BYTE:
                                featureIDAccessor = CesiumGltf::AccessorView<
                                    CesiumGltf::AccessorTypes::SCALAR<int8_t>>(gltf, *accessor);
                                break;
                            case CesiumGltf::Accessor::ComponentType::UNSIGNED_BYTE:
                                featureIDAccessor = CesiumGltf::AccessorView<
                                    CesiumGltf::AccessorTypes::SCALAR<uint8_t>>(
                                            gltf,
                                            *accessor);
                                break;
                            case CesiumGltf::Accessor::ComponentType::SHORT:
                                featureIDAccessor = CesiumGltf::AccessorView<
                                    CesiumGltf::AccessorTypes::SCALAR<int16_t>>(
                                            gltf,
                                            *accessor);
                                break;
                            case CesiumGltf::Accessor::ComponentType::UNSIGNED_SHORT:
                                featureIDAccessor = CesiumGltf::AccessorView<
                                    CesiumGltf::AccessorTypes::SCALAR<uint16_t>>(
                                            gltf,
                                            *accessor);
                                break;
                            case CesiumGltf::Accessor::ComponentType::FLOAT:
                                featureIDAccessor = CesiumGltf::AccessorView<
                                    CesiumGltf::AccessorTypes::SCALAR<float>>(gltf, *accessor);
                                break;
                            default:
                                continue;
                        }

                        std::string featureTableName = attribute.featureTable;
                        std::pair<std::string, AccessorType> p = {
                            featureTableName,
                            featureIDAccessor};
                        pairs.emplace_back(p);
                    }
                }
                }
                });
    }
}

void CesiumMetadataImpl::loadMetadata(
        const DotNet::CesiumForUnity::CesiumMetadata& metadata,
        const DotNet::UnityEngine::Transform& transform,
        int triangleIndex) {

    if (this->_featureTables.size() == 0) {
        loadMetadata();
    }

    this->_currentMetadataValues.clear();

    DotNet::CesiumForUnity::CesiumMetadata metadataScript =
        transform.gameObject()
        .GetComponentInParent<DotNet::CesiumForUnity::CesiumMetadata>();

    if (metadataScript != nullptr) {
        int32_t primitiveIndex = transform.GetSiblingIndex();
        if (primitiveIndex < _featureIDs.size()) {
            const std::pair<
                AccessorType,
                std::vector<std::pair<std::string, AccessorType>>>&
                    primitiveInfo = _featureIDs[primitiveIndex];
            const AccessorType& indicesAccessor = primitiveInfo.first;
            int64_t vertexIndex = std::visit(
                    [triangleIndex](auto&& value) {
                    return static_cast<int64_t>(value[triangleIndex].value[0]);
                    },
                    indicesAccessor);
            const auto& pairs = primitiveInfo.second;
            for (const auto& pair : pairs) {
                const std::string& featureTableName = pair.first;
                int64_t featureID = std::visit(
                        [vertexIndex](auto&& value) {
                        return static_cast<int64_t>(value[vertexIndex].value[0]);
                        },
                        pair.second);

                auto find = this->_featureTables.find(featureTableName);
                if (find != this->_featureTables.end()) {
                    const std::unordered_map<std::string, PropertyType>& featureTable =
                        find->second;
                    for (auto kvp : featureTable) {
                        const std::string& propertyName = kvp.first;
                        const PropertyType& propertyType = kvp.second;

                        ValueType value = std::visit(
                                [featureID](auto&& value) {
                                return static_cast<ValueType>(value.get(featureID));
                                },
                                propertyType);
                        std::pair<std::string, ValueType> p = {propertyName, value};
                        _currentMetadataValues.emplace_back(p);
                    }
                }
            }
        }
    }
}

void CesiumMetadataImpl::loadMetadata(
        const CesiumGltf::Model& model,
        const CesiumGltf::ExtensionModelExtFeatureMetadata& modelMetadata) {
    this->_pModel = &model;
    this->_pModelMetadata = &modelMetadata;
}
