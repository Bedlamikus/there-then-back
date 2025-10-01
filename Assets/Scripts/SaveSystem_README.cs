/*
 * ========================================
 * СИСТЕМА СОХРАНЕНИЯ - ДОКУМЕНТАЦИЯ
 * ========================================
 * 
 * Гибкая система сохранения на базе SaveData<T>
 * Каждая сущность управляет своими сохранениями через конструктор
 * Использует JSON сериализацию и PlayerPrefs
 * 
 * ========================================
 * БЫСТРЫЙ СТАРТ
 * ========================================
 * 
 * 1. Создайте класс данных (должен быть [Serializable]):
 * 
 *    [System.Serializable]
 *    public class MyEntityData
 *    {
 *        public float posX, posY, posZ;
 *        public int level = 1;
 *        
 *        // Обязательный конструктор по умолчанию!
 *        public MyEntityData() { }
 *    }
 * 
 * 
 * 2. В вашем MonoBehaviour создайте SaveData<T>:
 * 
 *    public class MyEntity : MonoBehaviour
 *    {
 *        private SaveData<MyEntityData> saveData;
 *        
 *        void Start()
 *        {
 *            // Создаем с именем GameObject (важно!)
 *            saveData = new SaveData<MyEntityData>(gameObject.name);
 *            
 *            // Загружаем если существует
 *            if (saveData.Exists())
 *                LoadMyData();
 *        }
 *        
 *        void LoadMyData()
 *        {
 *            var data = saveData.Load();
 *            transform.position = new Vector3(data.posX, data.posY, data.posZ);
 *            // ... применяем данные
 *        }
 *        
 *        void SaveMyData()
 *        {
 *            var data = saveData.Data;
 *            data.posX = transform.position.x;
 *            data.posY = transform.position.y;
 *            data.posZ = transform.position.z;
 *            saveData.Save();
 *        }
 *    }
 * 
 * 
 * ========================================
 * ОСНОВНЫЕ МЕТОДЫ SaveData<T>
 * ========================================
 * 
 * КОНСТРУКТОР:
 *   new SaveData<T>(string entityName)
 *   - entityName: имя сущности (обычно gameObject.name)
 * 
 * СВОЙСТВО:
 *   .Data - получить/установить данные (автоматически помечает как dirty)
 * 
 * СОХРАНЕНИЕ:
 *   .Save()         - сохранить данные в PlayerPrefs
 *   .SaveIfDirty()  - сохранить только если изменились
 *   .MarkDirty()    - пометить как измененные
 * 
 * ЗАГРУЗКА:
 *   .Load()         - загрузить данные из PlayerPrefs
 *   .Exists()       - проверить существование сохранения
 * 
 * УДАЛЕНИЕ:
 *   .Delete()       - удалить сохранение
 * 
 * ОТЛАДКА:
 *   .ToJson(bool prettyPrint = true)  - получить JSON для отладки
 *   .GetSaveKey()                     - получить ключ в PlayerPrefs
 *   .IsDirty()                        - проверить, изменены ли данные
 * 
 * 
 * ========================================
 * ГЛОБАЛЬНЫЙ SaveManager
 * ========================================
 * 
 * SaveManager.DeleteAllSaves()           - удалить все сохранения
 * SaveManager.DeleteSave(string name)    - удалить сохранение по имени
 * SaveManager.HasSave(string name)       - проверить существование
 * SaveManager.DebugPrintAllSaves()       - вывести список сохранений
 * 
 * 
 * ========================================
 * ПРИМЕРЫ ИСПОЛЬЗОВАНИЯ
 * ========================================
 * 
 * ПРИМЕР 1: Простое сохранение позиции
 * --------------------------------------
 * 
 * [System.Serializable]
 * public class PositionData
 * {
 *     public float x, y, z;
 *     public PositionData() { }
 * }
 * 
 * public class SaveableObject : MonoBehaviour
 * {
 *     private SaveData<PositionData> saveData;
 *     
 *     void Start()
 *     {
 *         saveData = new SaveData<PositionData>(gameObject.name);
 *         
 *         if (saveData.Exists())
 *         {
 *             var data = saveData.Load();
 *             transform.position = new Vector3(data.x, data.y, data.z);
 *         }
 *     }
 *     
 *     void OnDestroy()
 *     {
 *         var data = saveData.Data;
 *         data.x = transform.position.x;
 *         data.y = transform.position.y;
 *         data.z = transform.position.z;
 *         saveData.Save();
 *     }
 * }
 * 
 * 
 * ПРИМЕР 2: Автосохранение с интервалом
 * --------------------------------------
 * 
 * public class AutoSaveEntity : MonoBehaviour
 * {
 *     private SaveData<MyData> saveData;
 *     public float saveInterval = 5f;
 *     private float lastSaveTime;
 *     
 *     void Start()
 *     {
 *         saveData = new SaveData<MyData>(gameObject.name);
 *         saveData.Load();
 *         lastSaveTime = Time.time;
 *     }
 *     
 *     void Update()
 *     {
 *         if (Time.time - lastSaveTime >= saveInterval)
 *         {
 *             saveData.SaveIfDirty(); // Сохраняем только если изменилось
 *             lastSaveTime = Time.time;
 *         }
 *     }
 *     
 *     void OnApplicationQuit()
 *     {
 *         saveData.Save(); // Принудительно сохраняем при выходе
 *     }
 * }
 * 
 * 
 * ПРИМЕР 3: Комплексные данные
 * --------------------------------------
 * 
 * [System.Serializable]
 * public class PlayerComplexData
 * {
 *     public float[] position = new float[3];
 *     public int health = 100;
 *     public int level = 1;
 *     public List<string> inventory = new List<string>();
 *     
 *     public PlayerComplexData() { }
 *     
 *     public void SetPosition(Vector3 pos)
 *     {
 *         position[0] = pos.x;
 *         position[1] = pos.y;
 *         position[2] = pos.z;
 *     }
 * }
 * 
 * 
 * ========================================
 * ВАЖНЫЕ ЗАМЕЧАНИЯ
 * ========================================
 * 
 * 1. Класс данных ОБЯЗАТЕЛЬНО должен быть [System.Serializable]
 * 2. Класс данных ДОЛЖЕН иметь конструктор по умолчанию без параметров
 * 3. JsonUtility НЕ поддерживает:
 *    - Dictionary (используйте List или массивы)
 *    - Nullable типы
 *    - Свойства (только public поля)
 *    - Циклические ссылки
 * 4. Имя сущности (entityName) должно быть уникальным!
 * 5. PlayerPrefs имеет ограничение на размер (~1MB на платформу)
 * 
 * 
 * ========================================
 * ГОТОВЫЕ РЕАЛИЗАЦИИ
 * ========================================
 * 
 * VoxelWorld - сохранение состояния воксельного мира
 *   - Автосохранение каждые 30 секунд
 *   - Сохранение при выходе из игры
 *   - Context Menu: "Save World", "Load World", "Delete Save"
 * 
 * PlayerSaveDataExample - пример сохранения игрока
 *   - Показывает базовое использование
 *   - Автосохранение позиции и ротации
 * 
 * 
 * ========================================
 * ОТЛАДКА
 * ========================================
 * 
 * // Вывести JSON данных в консоль
 * Debug.Log(saveData.ToJson(prettyPrint: true));
 * 
 * // Проверить ключ в PlayerPrefs
 * Debug.Log(saveData.GetSaveKey());
 * 
 * // Проверить, изменены ли данные
 * Debug.Log(saveData.IsDirty());
 * 
 * // Удалить все сохранения (осторожно!)
 * SaveManager.DeleteAllSaves();
 * 
 * 
 * ========================================
 * РАСПОЛОЖЕНИЕ ФАЙЛОВ PlayerPrefs
 * ========================================
 * 
 * Windows:  HKCU\Software\[CompanyName]\[ProductName]
 * macOS:    ~/Library/Preferences/[bundle identifier].plist
 * Linux:    ~/.config/unity3d/[CompanyName]/[ProductName]
 * WebGL:    IndexedDB браузера
 * 
 */

// Этот файл содержит только документацию и не требует компиляции
// Но оставлен как .cs для удобства просмотра в Unity Editor

