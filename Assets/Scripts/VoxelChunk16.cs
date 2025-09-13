using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VoxelChunk16 : MonoBehaviour
{
    public const int WIDTH = 16;   // X
    public const int DEPTH = 16;   // Z
    public const int HEIGHT = 128;  // Y

    [Header("Atlas")]
    public Material atlasMaterial;
    [Range(1, 32)] public int atlasCols = 10; // 10 => шаг UV = 0.1
    public int[] typeToTileIndex = { 0, 1, 2, 3, 4, 5, 6, 7}; // 0=трава,1=земля,2=камень,3=песок,4=дерево
    public bool generateCollider = true;

    static readonly Vector3[] FaceNormal = {
        new( 0,  0,  1), // Front  Z+
        new( 0,  0, -1), // Back   Z-
        new( 0,  1,  0), // Top    Y+
        new( 0, -1,  0), // Bottom Y-
        new( 1,  0,  0), // Right  X+
        new(-1,  0,  0), // Left   X-
    };

    static readonly Vector3[][] FaceVerts = {
        new [] { new Vector3(0,0,1), new(1,0,1), new(1,1,1), new(0,1,1) }, // Z+
        new [] { new Vector3(1,0,0), new(0,0,0), new(0,1,0), new(1,1,0) }, // Z-
        new [] { new Vector3(0,1,1), new(1,1,1), new(1,1,0), new(0,1,0) }, // Y+
        new [] { new Vector3(0,0,0), new(1,0,0), new(1,0,1), new(0,0,1) }, // Y-
        new [] { new Vector3(1,0,1), new(1,0,0), new(1,1,0), new(1,1,1) }, // X+
        new [] { new Vector3(0,0,0), new(0,0,1), new(0,1,1), new(0,1,0) }, // X-
    };

    static readonly int[] QuadTris = { 0, 1, 2, 0, 2, 3 };

    Mesh _mesh;
    MeshCollider _collider;

    // Предкеш UV-тайла (u0,v0,u1,v1) на каждый тип блока
    struct TileUV { public float u0, v0, u1, v1; }
    TileUV[] _tileUVCache;
    float TileSize => 1f / atlasCols;

    // Вспомогательная функция: плоский индекс
    static int Idx(int x, int y, int z) => x + z * WIDTH + y * WIDTH * DEPTH;

    /// <summary>
    /// Строит меш по 3D-массиву типов. -1 = пусто.
    /// Ожидает массив размером [16,128,16] или [WIDTH,HEIGHT,DEPTH].
    /// </summary>
    public void Build(int[,,] data)
    {
        // базовые проверки
        if (data.GetLength(0) != WIDTH || data.GetLength(1) != HEIGHT || data.GetLength(2) != DEPTH)
        {
            Debug.LogError($"Неверный размер массива. Ожидается [{WIDTH},{HEIGHT},{DEPTH}] (X,Y,Z).");
            return;
        }

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "VoxelChunk16" };
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        // Предкеш UV для каждого встречающегося типа
        PrepareTileUvCache();

        // Оценим верхнюю границу по числу граней (грубая) и предвыделим память
        // Средняя заполняемость обычно < 50%, так что с запасом.
        int capacityVerts = WIDTH * HEIGHT * DEPTH * 12; // 2 треугольника * 3 вершины * коеф. редукции
        var verts = new List<Vector3>(capacityVerts);
        var uvs = new List<Vector2>(capacityVerts);
        var norms = new List<Vector3>(capacityVerts);
        var tris = new List<int>(capacityVerts * 3 / 2);

        // локальный метод: проверка твёрдого блока
        bool IsSolid(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= WIDTH || y >= HEIGHT || z >= DEPTH) return false;
            return data[x, y, z] >= 0;
        }

        // Основной цикл
        for (int y = 0; y < HEIGHT; y++)
            for (int z = 0; z < DEPTH; z++)
                for (int x = 0; x < WIDTH; x++)
                {
                    int type = data[x, y, z];
                    if (type < 0) continue;

                    // UV-границы тайла
                    var tu = GetTileUV(type);

                    // Проверяем шесть соседей; если сосед пуст — добавляем соответствующую грань
                    // порядок граней: Z+, Z-, Y+, Y-, X+, X-
                    // соседние координаты:
                    // Z+: (x,y,z+1), Z-: (x,y,z-1), Y+: (x,y+1,z), Y-: (x,y-1,z), X+: (x+1,y,z), X-: (x-1,y,z)
                    // на границе чанка считаем соседа пустым (грань видна)
                    // если будете стыковать чанки, замените проверку на просмотр соседнего чанка.
                    // -------------------------------------------
                    // Z+
                    if (!IsSolid(x, y, z + 1)) AddFace(0, x, y, z, tu, verts, uvs, norms, tris);
                    // Z-
                    if (!IsSolid(x, y, z - 1)) AddFace(1, x, y, z, tu, verts, uvs, norms, tris);
                    // Y+
                    if (!IsSolid(x, y + 1, z)) AddFace(2, x, y, z, tu, verts, uvs, norms, tris);
                    // Y-
                    if (!IsSolid(x, y - 1, z)) AddFace(3, x, y, z, tu, verts, uvs, norms, tris);
                    // X+
                    if (!IsSolid(x + 1, y, z)) AddFace(4, x, y, z, tu, verts, uvs, norms, tris);
                    // X-
                    if (!IsSolid(x - 1, y, z)) AddFace(5, x, y, z, tu, verts, uvs, norms, tris);
                }

        // Заливка в Mesh
        _mesh.Clear();
        _mesh.SetVertices(verts);
        _mesh.SetTriangles(tris, 0, true);
        _mesh.SetNormals(norms);
        _mesh.SetUVs(0, uvs);
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;
        var mr = GetComponent<MeshRenderer>();
        if (mr.sharedMaterial != atlasMaterial) mr.sharedMaterial = atlasMaterial;

        if (generateCollider)
        {
            if (_collider == null)
            {
                _collider = gameObject.GetComponent<MeshCollider>();
                if (_collider == null) _collider = gameObject.AddComponent<MeshCollider>();
            }

            _collider.sharedMesh = null; // форс обновление
            _collider.sharedMesh = _mesh;
        }
    }

    // ==== вспомогалки ====

    void PrepareTileUvCache()
    {
        if (_tileUVCache == null || _tileUVCache.Length != typeToTileIndex.Length)
            _tileUVCache = new TileUV[typeToTileIndex.Length];

        float step = TileSize;
        for (int i = 0; i < typeToTileIndex.Length; i++)
        {
            int tile = (i >= 0 && i < typeToTileIndex.Length) ? typeToTileIndex[i] : 0;
            int uIdx = tile % atlasCols;
            int vIdx = tile / atlasCols;

            // лёгкое «сжатие» UV внутрь тайла, чтобы избежать мип-кровотечения
            const float eps = 0.001f;
            float u0 = uIdx * step + eps;
            float v0 = vIdx * step + eps;
            float u1 = (uIdx + 1) * step - eps;
            float v1 = (vIdx + 1) * step - eps;

            _tileUVCache[i] = new TileUV { u0 = u0, v0 = v0, u1 = u1, v1 = v1 };
        }
    }

    TileUV GetTileUV(int type)
    {
        if (type < 0 || type >= _tileUVCache.Length) return _tileUVCache[0];
        return _tileUVCache[type];
    }

    void AddFace(
        int faceIndex, int x, int y, int z, TileUV tu,
        List<Vector3> verts, List<Vector2> uvs, List<Vector3> norms, List<int> tris)
    {
        int baseIndex = verts.Count;
        var fv = FaceVerts[faceIndex];

        verts.Add(new Vector3(x, y, z) + fv[0]);
        verts.Add(new Vector3(x, y, z) + fv[1]);
        verts.Add(new Vector3(x, y, z) + fv[2]);
        verts.Add(new Vector3(x, y, z) + fv[3]);

        // плоские нормали
        Vector3 n = FaceNormal[faceIndex];
        norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);

        // UV в порядке вершин fv
        uvs.Add(new Vector2(tu.u0, tu.v0));
        uvs.Add(new Vector2(tu.u1, tu.v0));
        uvs.Add(new Vector2(tu.u1, tu.v1));
        uvs.Add(new Vector2(tu.u0, tu.v1));

        tris.Add(baseIndex + QuadTris[0]);
        tris.Add(baseIndex + QuadTris[1]);
        tris.Add(baseIndex + QuadTris[2]);
        tris.Add(baseIndex + QuadTris[3]);
        tris.Add(baseIndex + QuadTris[4]);
        tris.Add(baseIndex + QuadTris[5]);
    }
}