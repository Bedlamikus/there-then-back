using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VoxelChunk16 : MonoBehaviour
{
    public const int WIDTH = 16;
    public const int HEIGHT = 128;
    public const int DEPTH = 16;

    public Material atlasMaterial;
    public bool generateCollider = true;

    // карта соответствия типов → базовый индекс тайла в атласе
    public int[] typeToTileIndex = new int[256];

    // ===== для повреждённых тайлов =====
    public short[,,] hpData;                   // ссылка на HP-массив из VoxelWorld
    public bool useDamageTiles = true;
    public int damageStates = 5;               // 5 состояний (0..4)
    public int[] typeMaxHpLut = new int[256];  // max HP по типам

    // ===== приватное =====
    Mesh _mesh;
    MeshCollider _collider;

    // подготовленные UV-координаты (кеш на каждый tileIndex)
    static readonly Dictionary<int, Vector2[]> uvCache = new();
    const int atlasCols = 10; // у тебя атлас 10x10

    public void Build(int[,,] data)
    {
        if (_mesh == null)
        {
            _mesh = new Mesh();
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            GetComponent<MeshFilter>().mesh = _mesh;
            GetComponent<MeshRenderer>().material = atlasMaterial;
        }

        if (_collider == null && generateCollider)
        {
            _collider = gameObject.GetComponent<MeshCollider>();
            if (_collider == null) _collider = gameObject.AddComponent<MeshCollider>();
        }

        var verts = new List<Vector3>();
        var tris = new List<int>();
        var uvs = new List<Vector2>();

        for (int x = 0; x < WIDTH; x++)
            for (int y = 0; y < HEIGHT; y++)
                for (int z = 0; z < DEPTH; z++)
                {
                    int type = data[x, y, z];
                    if (type == -1) continue;

                    // --- здесь решаем индекс тайла (с учётом повреждений) ---
                    int tileIndex = GetTileIndexWithDamage(type, x, y, z);

                    // соседние блоки
                    for (int face = 0; face < 6; face++)
                    {
                        Vector3Int dir = VoxelData.dirs[face];
                        int nx = x + dir.x;
                        int ny = y + dir.y;
                        int nz = z + dir.z;

                        bool neighborSolid =
                            nx >= 0 && nx < WIDTH &&
                            ny >= 0 && ny < HEIGHT &&
                            nz >= 0 && nz < DEPTH &&
                            data[nx, ny, nz] != -1;

                        if (!neighborSolid)
                        {
                            int vIndex = verts.Count;

                            for (int i = 0; i < 4; i++)
                                verts.Add(new Vector3(x, y, z) + VoxelData.faceVerts[face, i]);

                            tris.Add(vIndex + 0);
                            tris.Add(vIndex + 1);
                            tris.Add(vIndex + 2);
                            tris.Add(vIndex + 2);
                            tris.Add(vIndex + 1);
                            tris.Add(vIndex + 3);

                            var faceUvs = GetTileUvs(tileIndex);
                            uvs.AddRange(faceUvs);
                        }
                    }
                }

        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetTriangles(tris, 0);
        _mesh.SetUVs(0, uvs);
        _mesh.RecalculateNormals();

        if (generateCollider && _collider != null)
        {
            _collider.sharedMesh = null;
            _collider.sharedMesh = _mesh;
        }
    }

    // ===== Helpers =====

    int GetTileIndexWithDamage(int type, int x, int y, int z)
    {
        int baseIndex = (type >= 0 && type < typeToTileIndex.Length) ? typeToTileIndex[type] : 0;

        if (!useDamageTiles || hpData == null) return baseIndex;

        int maxHp = (type >= 0 && type < typeMaxHpLut.Length) ? typeMaxHpLut[type] : 0;
        if (maxHp <= 0) return baseIndex;

        int curHp = Mathf.Clamp(hpData[x, y, z], 0, maxHp);
        float ratio = (float)curHp / maxHp;

        int state;
        if (ratio >= 0.80f) state = 0;
        else if (ratio >= 0.50f) state = 1;
        else if (ratio >= 0.35f) state = 2;
        else if (ratio >= 0.20f) state = 3;
        else state = 4;

        return baseIndex + state * atlasCols;
    }

    Vector2[] GetTileUvs(int tileIndex)
    {
        if (uvCache.TryGetValue(tileIndex, out var cached)) return cached;

        int tx = tileIndex % atlasCols;
        int ty = tileIndex / atlasCols;

        float uvSize = 1f / atlasCols;
        float eps = 0.001f;

        float u0 = tx * uvSize + eps;
        float v0 = ty * uvSize + eps;
        float u1 = (tx + 1) * uvSize - eps;
        float v1 = (ty + 1) * uvSize - eps;

        var uv = new Vector2[4]
        {
            new Vector2(u0, v0),
            new Vector2(u1, v0),
            new Vector2(u0, v1),
            new Vector2(u1, v1)
        };

        uvCache[tileIndex] = uv;
        return uv;
    }
}

// Вспомогательные данные граней куба
public static class VoxelData
{
    public static readonly Vector3Int[] dirs = {
        new Vector3Int( 1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int( 0, 1, 0),
        new Vector3Int( 0,-1, 0),
        new Vector3Int( 0, 0, 1),
        new Vector3Int( 0, 0,-1)
    };

    public static readonly Vector3[,] faceVerts = {
        { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,0,1), new Vector3(1,1,1) }, // +X
        { new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,0), new Vector3(0,1,1) }, // -X
        { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,0), new Vector3(1,1,1) }, // +Y
        { new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(0,0,1), new Vector3(1,0,1) }, // -Y
        { new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(0,1,1), new Vector3(1,1,1) }, // +Z
        { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,0,0), new Vector3(1,1,0) }  // -Z
    };
}
