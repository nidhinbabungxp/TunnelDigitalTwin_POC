using UnityEngine;

public class IMUChestData : MonoBehaviour
{
    public IMUDataSource source; // Reference to the IMUDataSource script
    public Transform kevin; // Transform of the chest object

public float posOffset = 0.0f; // Offset for the chest position along the forward axis
    // Additional logic to update the above properties based on IMU data can be added here.

     void LateUpdate()
    {
        print("Chest Position: " + source.ChestPosition);
       var adjustedPosition = source.ChestPosition + transform.forward * posOffset;
       transform.position = adjustedPosition;
    }

    
}
