﻿using System.IO;

public class VTXParser
{
    Stream stream;

    public vtxheader_t header;
    public SourceVtxBodyPart[] bodyParts;

    SourceVtxMesh theFirstMeshWithStripGroups;
    long theFirstMeshWithStripGroupsInputFileStreamPosition;
    SourceVtxMesh theSecondMeshWithStripGroups;
    long theExpectedStartOfSecondStripGroupList;
    bool theStripGroupUsesExtra8Bytes;

    public VTXParser(Stream stream)
    {
        this.stream = stream;
    }

    public vtxheader_t ReadSourceVtxHeader()
    {
        header = new vtxheader_t();

        header.version = DataParser.ReadInt(stream);
        header.vertCacheSize = DataParser.ReadInt(stream);
        header.maxBonesPerStrip = DataParser.ReadUShort(stream);
        header.maxBonesPerFace = DataParser.ReadUShort(stream);
        header.maxBonesPerVert = DataParser.ReadInt(stream);
        header.checkSum = DataParser.ReadInt(stream);
        header.numLODs = DataParser.ReadInt(stream);
        header.materialReplacementListOffset = DataParser.ReadInt(stream);
        header.numBodyParts = DataParser.ReadInt(stream);
        header.bodyPartOffset = DataParser.ReadInt(stream);

        return header;
    }

    public SourceVtxBodyPart[] ReadSourceVtxBodyParts()
    {
        if (header.numBodyParts > 0)
        {
            theFirstMeshWithStripGroups = null;
            theFirstMeshWithStripGroupsInputFileStreamPosition = -1;
            theSecondMeshWithStripGroups = null;
            theExpectedStartOfSecondStripGroupList = -1;
            theStripGroupUsesExtra8Bytes = false;

            long bodyPartInputFileStreamPosition;
            long inputFileStreamPosition;

            stream.Position = header.bodyPartOffset;
            bodyParts = new SourceVtxBodyPart[header.numBodyParts];
            for (int i = 0; i < bodyParts.Length; i++)
            {
                bodyPartInputFileStreamPosition = stream.Position;

                bodyParts[i] = new SourceVtxBodyPart();
                bodyParts[i].modelCount = DataParser.ReadInt(stream);
                bodyParts[i].modelOffset = DataParser.ReadInt(stream);

                inputFileStreamPosition = stream.Position;

                if (bodyParts[i].modelCount > 0 && bodyParts[i].modelOffset != 0)
                {
                    ReadSourceVtxModels(bodyPartInputFileStreamPosition, bodyParts[i]);
                }

                stream.Position = inputFileStreamPosition;
            }
        }

        return bodyParts;
    }
    private void ReadSourceVtxModels(long bodyPartInputFileStreamPosition, SourceVtxBodyPart aBodyPart)
    {
        long modelInputFileStreamPosition;
        long inputFileStreamPosition;

        stream.Position = bodyPartInputFileStreamPosition + aBodyPart.modelOffset;
        aBodyPart.theVtxModels = new SourceVtxModel[aBodyPart.modelCount];

        for (int i = 0; i < aBodyPart.theVtxModels.Length; i++)
        {
            modelInputFileStreamPosition = stream.Position;
            aBodyPart.theVtxModels[i] = new SourceVtxModel();
            aBodyPart.theVtxModels[i].lodCount = DataParser.ReadInt(stream);
            aBodyPart.theVtxModels[i].lodOffset = DataParser.ReadInt(stream);

            inputFileStreamPosition = stream.Position;
            if (aBodyPart.theVtxModels[i].lodCount > 0 && aBodyPart.theVtxModels[i].lodOffset != 0)
            {
                ReadSourceVtxModelLods(modelInputFileStreamPosition, aBodyPart.theVtxModels[i]);
            }

            stream.Position = inputFileStreamPosition;
        }
    }
    private void ReadSourceVtxModelLods(long modelInputFileStreamPosition, SourceVtxModel aModel)
    {
        long modelLodInputFileStreamPosition;
        long inputFileStreamPosition;

        stream.Position = modelInputFileStreamPosition + aModel.lodOffset;
        aModel.theVtxModelLods = new SourceVtxModelLod[aModel.lodCount];

        for (int i = 0; i < aModel.theVtxModelLods.Length; i++)
        {
            modelLodInputFileStreamPosition = stream.Position;
            aModel.theVtxModelLods[i] = new SourceVtxModelLod();
            aModel.theVtxModelLods[i].meshCount = DataParser.ReadInt(stream);
            aModel.theVtxModelLods[i].meshOffset = DataParser.ReadInt(stream);
            aModel.theVtxModelLods[i].switchPoint = DataParser.ReadFloat(stream);

            inputFileStreamPosition = stream.Position;
            if (aModel.theVtxModelLods[i].meshCount > 0 && aModel.theVtxModelLods[i].meshOffset != 0)
            {
                ReadSourceVtxMeshes(modelLodInputFileStreamPosition, aModel.theVtxModelLods[i]);
            }

            stream.Position = inputFileStreamPosition;
        }
    }
    private void ReadSourceVtxMeshes(long modelLodInputFileStreamPosition, SourceVtxModelLod aModelLod)
    {
        long meshInputFileStreamPosition;
        long inputFileStreamPosition;

        stream.Position = modelLodInputFileStreamPosition + aModelLod.meshOffset;
        aModelLod.theVtxMeshes = new SourceVtxMesh[aModelLod.meshCount];
        for (int i = 0; i < aModelLod.theVtxMeshes.Length; i++)
        {
            meshInputFileStreamPosition = stream.Position;
            aModelLod.theVtxMeshes[i] = new SourceVtxMesh();
            aModelLod.theVtxMeshes[i].stripGroupCount = DataParser.ReadInt(stream);
            aModelLod.theVtxMeshes[i].stripGroupOffset = DataParser.ReadInt(stream);
            aModelLod.theVtxMeshes[i].flags = DataParser.ReadByte(stream);

            inputFileStreamPosition = stream.Position;
            if (aModelLod.theVtxMeshes[i].stripGroupCount > 0 && aModelLod.theVtxMeshes[i].stripGroupOffset != 0)
            {
                if (theFirstMeshWithStripGroups == null)
                {
                    theFirstMeshWithStripGroups = aModelLod.theVtxMeshes[i];
                    theFirstMeshWithStripGroupsInputFileStreamPosition = meshInputFileStreamPosition;
                    AnalyzeVtxStripGroups(meshInputFileStreamPosition, aModelLod.theVtxMeshes[i]);
                    ReadSourceVtxStripGroups(meshInputFileStreamPosition, aModelLod.theVtxMeshes[i]);
                }
                else if (theSecondMeshWithStripGroups == null)
                {
                    theSecondMeshWithStripGroups = aModelLod.theVtxMeshes[i];
                    if (theExpectedStartOfSecondStripGroupList != (meshInputFileStreamPosition + aModelLod.theVtxMeshes[i].stripGroupOffset))
                    {
                        theStripGroupUsesExtra8Bytes = true;
                        if (aModelLod.theVtxMeshes[i].theVtxStripGroups != null)
                        {
                            aModelLod.theVtxMeshes[i] = null;
                        }

                        ReadSourceVtxStripGroups(theFirstMeshWithStripGroupsInputFileStreamPosition, theFirstMeshWithStripGroups);
                    }
                    ReadSourceVtxStripGroups(meshInputFileStreamPosition, aModelLod.theVtxMeshes[i]);
                }
                else
                {
                    ReadSourceVtxStripGroups(meshInputFileStreamPosition, aModelLod.theVtxMeshes[i]);
                }
            }

            stream.Position = inputFileStreamPosition;
        }
    }
    private void AnalyzeVtxStripGroups(long meshInputFileStreamPosition, SourceVtxMesh aMesh)
    {
        stream.Position = meshInputFileStreamPosition + aMesh.stripGroupOffset;
        aMesh.theVtxStripGroups = new SourceVtxStripGroup[aMesh.stripGroupCount];
        for (int i = 0; i < aMesh.theVtxStripGroups.Length; i++)
        {
            aMesh.theVtxStripGroups[i] = new SourceVtxStripGroup();
            aMesh.theVtxStripGroups[i].vertexCount = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].vertexOffset = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].indexCount = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].indexOffset = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].stripCount = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].stripOffset = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].flags = DataParser.ReadByte(stream);
        }
        theExpectedStartOfSecondStripGroupList = stream.Position;
    }
    private void ReadSourceVtxStripGroups(long meshInputFileStreamPosition, SourceVtxMesh aMesh)
    {
        long stripGroupInputFileStreamPosition;
        long inputFileStreamPosition;

        stream.Position = meshInputFileStreamPosition + aMesh.stripGroupOffset;
        aMesh.theVtxStripGroups = new SourceVtxStripGroup[aMesh.stripGroupCount];
        for (int i = 0; i < aMesh.theVtxStripGroups.Length; i++)
        {
            stripGroupInputFileStreamPosition = stream.Position;
            aMesh.theVtxStripGroups[i] = new SourceVtxStripGroup();
            aMesh.theVtxStripGroups[i].vertexCount = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].vertexOffset = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].indexCount = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].indexOffset = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].stripCount = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].stripOffset = DataParser.ReadInt(stream);
            aMesh.theVtxStripGroups[i].flags = DataParser.ReadByte(stream);

            if (theStripGroupUsesExtra8Bytes)
            {
                DataParser.ReadInt(stream);
                DataParser.ReadInt(stream);
            }

            inputFileStreamPosition = stream.Position;
            if (aMesh.theVtxStripGroups[i].vertexCount > 0 && aMesh.theVtxStripGroups[i].vertexOffset != 0)
            {
                ReadSourceVtxVertices(stripGroupInputFileStreamPosition, aMesh.theVtxStripGroups[i]);
            }
            if (aMesh.theVtxStripGroups[i].indexCount > 0 && aMesh.theVtxStripGroups[i].indexOffset != 0)
            {
                ReadSourceVtxIndices(stripGroupInputFileStreamPosition, aMesh.theVtxStripGroups[i]);
            }
            if (aMesh.theVtxStripGroups[i].stripCount > 0 && aMesh.theVtxStripGroups[i].stripOffset != 0)
            {
                ReadSourceVtxStrips(stripGroupInputFileStreamPosition, aMesh.theVtxStripGroups[i]);
            }
            stream.Position = inputFileStreamPosition;
        }
    }
    private void ReadSourceVtxVertices(long stripGroupInputFileStreamPosition, SourceVtxStripGroup aStripGroup)
    {
        stream.Position = stripGroupInputFileStreamPosition + aStripGroup.vertexOffset;
        aStripGroup.theVtxVertices = new SourceVtxVertex[aStripGroup.vertexCount];
        for (int i = 0; i < aStripGroup.theVtxVertices.Length; i++)
        {
            aStripGroup.theVtxVertices[i] = new SourceVtxVertex();
            aStripGroup.theVtxVertices[i].boneWeightIndex = new byte[VVDParser.MAX_NUM_BONES_PER_VERT];
            for (int j = 0; j < aStripGroup.theVtxVertices[i].boneWeightIndex.Length; j++)
            {
                aStripGroup.theVtxVertices[i].boneWeightIndex[j] = DataParser.ReadByte(stream);
            }

            aStripGroup.theVtxVertices[i].boneCount = DataParser.ReadByte(stream);
            aStripGroup.theVtxVertices[i].originalMeshVertexIndex = DataParser.ReadUShort(stream);

            aStripGroup.theVtxVertices[i].boneId = new byte[VVDParser.MAX_NUM_BONES_PER_VERT];
            for (int j = 0; j < aStripGroup.theVtxVertices[i].boneId.Length; j++)
            {
                aStripGroup.theVtxVertices[i].boneId[j] = DataParser.ReadByte(stream);
            }
        }
    }
    private void ReadSourceVtxIndices(long stripGroupInputFileStreamPosition, SourceVtxStripGroup aStripGroup)
    {
        stream.Position = stripGroupInputFileStreamPosition + aStripGroup.indexOffset;
        aStripGroup.theVtxIndices = new ushort[aStripGroup.indexCount];
        for (int i = 0; i < aStripGroup.theVtxIndices.Length; i++)
        {
            aStripGroup.theVtxIndices[i] = DataParser.ReadUShort(stream);
        }
    }
    private void ReadSourceVtxStrips(long stripGroupInputFileStreamPosition, SourceVtxStripGroup aStripGroup)
    {
        stream.Position = stripGroupInputFileStreamPosition + aStripGroup.stripOffset;
        aStripGroup.theVtxStrips = new SourceVtxStrip[aStripGroup.stripCount];
        for (int i = 0; i < aStripGroup.theVtxStrips.Length; i++)
        {
            aStripGroup.theVtxStrips[i] = new SourceVtxStrip();
            aStripGroup.theVtxStrips[i].indexCount = DataParser.ReadInt(stream);
            aStripGroup.theVtxStrips[i].indexMeshIndex = DataParser.ReadInt(stream);
            aStripGroup.theVtxStrips[i].vertexCount = DataParser.ReadInt(stream);
            aStripGroup.theVtxStrips[i].vertexMeshIndex = DataParser.ReadInt(stream);
            aStripGroup.theVtxStrips[i].boneCount = DataParser.ReadShort(stream);
            aStripGroup.theVtxStrips[i].flags = DataParser.ReadByte(stream);
            aStripGroup.theVtxStrips[i].boneStateChangeCount = DataParser.ReadInt(stream);
            aStripGroup.theVtxStrips[i].boneStateChangeOffset = DataParser.ReadInt(stream);
        }
    }
}