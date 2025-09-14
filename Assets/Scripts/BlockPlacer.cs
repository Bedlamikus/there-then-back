using UnityEngine;

public class BlockPlacer : MonoBehaviour
{
    [Header("Input")]
    public Camera cam;                     // если пусто — возьмём Camera.main
    public float rayDistance = 100f;

    [Header("Palette (ПКМ — переключить)")]
    public int[] palette = { 0, 1, 2, 6, 7 }; // 0=трава,1=земля,2=камень,6=уголь,7=золото
    public int selectedIndex = 0;

    [Header("UI")]
    public bool showHud = true;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (cam == null || VoxelWorld.Instance == null) return;

        // ПКМ — переключение блока
        if (Input.GetMouseButtonDown(1))
        {
            selectedIndex = (selectedIndex + 1) % palette.Length;
        }

        // ЛКМ — попытка поставить
        if (Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit, rayDistance))
            {
                int blockType = palette[selectedIndex];
                VoxelWorld.Instance.PlaceAdjacent(hit, blockType);
            }
        }

        // (опционально) колесо мыши — тоже переключение
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.1f)
        {
            selectedIndex = (selectedIndex - (int)Mathf.Sign(scroll)) % palette.Length;
            if (selectedIndex < 0) selectedIndex += palette.Length;
        }
    }

    // Простенький HUD
    void OnGUI()
    {
        if (!showHud) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
        string[] names = { "Трава(0)", "Земля(1)", "Камень(2)", "Уголь(6)", "Золото(7)" };

        int t = palette[selectedIndex];
        string name = (t == 0) ? names[0] :
                      (t == 1) ? names[1] :
                      (t == 2) ? names[2] :
                      (t == 6) ? names[3] :
                      (t == 7) ? names[4] : $"Тип {t}";

        GUI.Box(new Rect(10, 10, 220, 28), $"ЛКМ: поставить | ПКМ/скролл: выбрать → {name}", style);
    }
}
