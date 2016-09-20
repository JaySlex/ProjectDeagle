﻿using UnityEngine;
using System.IO;

public class VVDParser
{
    public const int MAX_NUM_LODS = 7, MAX_NUM_BONES_PER_VERT = 3;
    Stream stream;
    public vertexFileHeader_t header;
    public vertexFileFixup_t[] fileFixup;
    public mstudiovertex_t[][] vertices;

    public VVDParser(Stream stream)
    {
        this.stream = stream;
    }

    public vertexFileHeader_t ParseHeader()
    {
        header = new vertexFileHeader_t();

        header.id = DataParser.ReadInt(stream);
        header.version = DataParser.ReadInt(stream); // MODEL_VERTEX_FILE_VERSION
        header.checksum = DataParser.ReadLong(stream); // same as studiohdr_t, ensures sync
        header.numLODs = DataParser.ReadInt(stream); // num of valid lods

        header.numLODVertices = new int[MAX_NUM_LODS]; // num verts for desired root lod (size is MAX_NUM_LODS = 8)
        for(int i = 0; i < header.numLODVertices.Length; i++)
        {
            header.numLODVertices[i] = DataParser.ReadInt(stream);
        }

        header.numFixups = DataParser.ReadInt(stream); // num of vertexFileFixup_t
        header.fixupTableStart = DataParser.ReadInt(stream); // offset from base to fixup table
        header.vertexDataStart = DataParser.ReadInt(stream); // offset from base to vertex block
        header.tangentDataStart = DataParser.ReadInt(stream); // offset from base to tangent block

        return header;
    }
    public vertexFileFixup_t[] ParseFixupTable()
    {
        fileFixup = new vertexFileFixup_t[header.numFixups];

        stream.Position = header.fixupTableStart;
        for (int i = 0; i < fileFixup.Length; i++)
        {
            fileFixup[i].lod = DataParser.ReadInt(stream); // used to skip culled root lod
            fileFixup[i].sourceVertexID = DataParser.ReadInt(stream); // absolute index from start of vertex/tangent blocks
            fileFixup[i].numVertices = DataParser.ReadInt(stream);
        }

        return fileFixup;
    }
    public mstudiovertex_t[][] ParseVertices()
    {
        if(header.numLODs > 0)
        {
            stream.Position = header.vertexDataStart;

            vertices = new mstudiovertex_t[header.numLODVertices.Length][];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new mstudiovertex_t[header.numLODVertices[i]];
                for (int j = 0; j < vertices[i].Length; j++)
                {
                    vertices[i][j].m_BoneWeights.weight = new float[MAX_NUM_BONES_PER_VERT];
                    for (int k = 0; k < vertices[i][j].m_BoneWeights.weight.Length; k++)
                    {
                        vertices[i][j].m_BoneWeights.weight[k] = DataParser.ReadFloat(stream);
                    }
                    vertices[i][j].m_BoneWeights.bone = new char[MAX_NUM_BONES_PER_VERT];
                    for (int k = 0; k < vertices[i][j].m_BoneWeights.bone.Length; k++)
                    {
                        vertices[i][j].m_BoneWeights.bone[k] = DataParser.ReadChar(stream);
                    }
                    vertices[i][j].m_BoneWeights.numbones = DataParser.ReadByte(stream);

                    vertices[i][j].m_vecPosition = new Vector3(DataParser.ReadFloat(stream), DataParser.ReadFloat(stream), DataParser.ReadFloat(stream));
                    vertices[i][j].m_vecPosition = new Vector3(vertices[i][j].m_vecPosition.x, vertices[i][j].m_vecPosition.z, vertices[i][j].m_vecPosition.y);
                    vertices[i][j].m_vecNormal = new Vector3(DataParser.ReadFloat(stream), DataParser.ReadFloat(stream), DataParser.ReadFloat(stream));
                    vertices[i][j].m_vecNormal = new Vector3(vertices[i][j].m_vecNormal.x, vertices[i][j].m_vecNormal.z, vertices[i][j].m_vecNormal.y);
                    vertices[i][j].m_vecTexCoord = new Vector2(DataParser.ReadFloat(stream), DataParser.ReadFloat(stream));
                    vertices[i][j].m_vecTexCoord = new Vector2(vertices[i][j].m_vecTexCoord.x, 1 - vertices[i][j].m_vecTexCoord.y);
                }
            }
        }

        return vertices;
    }
}