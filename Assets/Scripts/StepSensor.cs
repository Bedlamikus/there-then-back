using UnityEngine;

public class StepSensor : MonoBehaviour
{
    public LayerMask solidMask = ~0;

    public bool IsBlocked { get; private set; }
    int _touchCount;
    Transform _root;               // корень игрока
    Collider[] _rootCols;          // все коллайдеры игрока/сенсоров

    void Awake()
    {
        _root = transform.root;
        _rootCols = _root.GetComponentsInChildren<Collider>(true);
    }

    void OnEnable() { _touchCount = 0; IsBlocked = false; }

    bool IsSolid(Collider col)
    {
        if (col == null) return false;
        if (col.isTrigger) return false;
        if (((1 << col.gameObject.layer) & solidMask) == 0) return false;

        // исключаем свои/родительские коллайдеры
        for (int i = 0; i < _rootCols.Length; i++)
            if (col == _rootCols[i]) return false;

        return true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsSolid(other)) return;
        _touchCount++; IsBlocked = _touchCount > 0;
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsSolid(other)) return;
        _touchCount = Mathf.Max(0, _touchCount - 1);
        if (_touchCount == 0) IsBlocked = false;
    }
}
