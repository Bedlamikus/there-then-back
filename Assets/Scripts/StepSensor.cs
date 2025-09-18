using UnityEngine;

public class StepSensor : MonoBehaviour
{
    public bool IsBlocked;

    private void OnTriggerStay(Collider other)
    {
        IsBlocked = true;
    }

    void OnTriggerExit(Collider other)
    {
        IsBlocked = false;
    }
}
