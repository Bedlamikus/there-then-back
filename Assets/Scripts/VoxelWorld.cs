using System.Collections.Generic;
using UnityEngine;

public class VoxelWorld : MonoBehaviour
{
    public static VoxelWorld Instance { get; private set; }

    [Header("Chunk grid")]
    public int chunksX = 5;
    public int chunksZ = 5;

    [Header("Render/Physics")]
    public Material atlasMaterial;
    public bool generateColliders = true;

    // ��������� ������ (���������� ��� ��������� ����/��� � �.�.)
    [Header("Data generator")]
    public VoxelWorldGenerator generator; // ������� � ���������� (����� �� ���� �� ������)

    public class ChunkEntry
    {
        public int cx, cz;
        public int[,,] data;
        public VoxelChunk16 builder;
        public GameObject go;
    }

    private readonly Dictionary<(int cx, int cz), ChunkEntry> _chunks = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // ���� ��������� �� �������� � ��������� ����� �� ���� �� �������
        if (generator == null) generator = GetComponent<VoxelWorldGenerator>();
        if (generator == null) generator = gameObject.AddComponent<VoxelWorldGenerator>();
    }

    [ContextMenu("Generate world")]
    public void Generate()
    {
        // �������� ������ �����
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        _chunks.Clear();

        // ������������� �����
        for (int cz = 0; cz < chunksZ; cz++)
            for (int cx = 0; cx < chunksX; cx++)
            {
                GenerateChunk(cx, cz);
            }
    }

    void GenerateChunk(int cx, int cz)
    {
        // ������ ����� �� ����������
        int[,,] data = generator.BuildChunkData(cx, cz);

        // ������ + ���
        var go = new GameObject($"Chunk({cx},{cz})");
        go.transform.parent = transform;
        go.transform.position = new Vector3(cx * VoxelChunk16.WIDTH, 0, cz * VoxelChunk16.DEPTH);

        var builder = go.AddComponent<VoxelChunk16>();
        builder.atlasMaterial = atlasMaterial;
        builder.generateCollider = generateColliders;
        builder.Build(data);

        // ������������
        _chunks[(cx, cz)] = new ChunkEntry
        {
            cx = cx,
            cz = cz,
            data = data,
            builder = builder,
            go = go
        };
    }

    // === ��������� API: �������� ����� ===
    public void CarveSphere(Vector3 worldPos, float radius)
    {
        if (_chunks.Count == 0) return;

        float r2 = radius * radius;

        int minX = Mathf.FloorToInt(worldPos.x - radius);
        int maxX = Mathf.CeilToInt(worldPos.x + radius);
        int minY = Mathf.Max(0, Mathf.FloorToInt(worldPos.y - radius));
        int maxY = Mathf.Min(VoxelChunk16.HEIGHT - 1, Mathf.CeilToInt(worldPos.y + radius));
        int minZ = Mathf.FloorToInt(worldPos.z - radius);
        int maxZ = Mathf.CeilToInt(worldPos.z + radius);

        var touched = new HashSet<(int cx, int cz)>();

        for (int y = minY; y <= maxY; y++)
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                {
                    // ������� ���� (�� X/Z)
                    if (x < 0 || z < 0) continue;
                    if (x >= chunksX * VoxelChunk16.WIDTH) continue;
                    if (z >= chunksZ * VoxelChunk16.DEPTH) continue;

                    // �������� ����� (�� ������� ��������)
                    float dx = (x + 0.5f) - worldPos.x;
                    float dy = (y + 0.5f) - worldPos.y;
                    float dz = (z + 0.5f) - worldPos.z;
                    if (dx * dx + dy * dy + dz * dz > r2) continue;

                    // ������� -> ����/��������� �������
                    int cxi = x / VoxelChunk16.WIDTH;
                    int czi = z / VoxelChunk16.DEPTH;
                    int lx = x % VoxelChunk16.WIDTH;
                    int lz = z % VoxelChunk16.DEPTH;

                    if (_chunks.TryGetValue((cxi, czi), out var entry))
                    {
                        if (entry.data[lx, y, lz] != -1)
                        {
                            entry.data[lx, y, lz] = -1; // ������
                            touched.Add((cxi, czi));
                        }
                    }
                }

        // ����������� ������ ����������
        foreach (var key in touched)
        {
            var entry = _chunks[key];
            entry.builder.Build(entry.data);
        }
    }
}
