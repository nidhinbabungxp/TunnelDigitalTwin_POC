using ControlRig;
using Unity.VisualScripting;
using UnityEngine;

/// Drives another Animation Rigging character's targets from any IMotionSource.
/// Attach to the receiver rigged character. 
/// Drag the source (TargetDataSource) and this character's Rig_References + targets.
public class RigReceiver : MonoBehaviour
{
    [Header("Source � drag the GameObject with TargetDataSource or DataSource")]
    public MonoBehaviour sourceObject;  // Must implement IMotionSource
    public Animator animator;
    [Header("This character's Animation Rigging targets to drive")]
    public Transform chestTarget;
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header("This character's root chest reference (for offset calculation)")]
    public Transform localChestRef;  // The actual chest bone or a reference point
    public bool useChestBoneAsChestRef = false; // If true, will find chest bone automatically and ignore localChestRef
    [Header("Shoulder bones (for arm reach clamping)")]
    [Tooltip("Drag the LeftUpperArm bone from the rig hierarchy")]
    public Transform leftShoulderBone;
    [Tooltip("Drag the RightUpperArm bone from the rig hierarchy")]
    public Transform rightShoulderBone;

    [Header("Arm Reach Constraint")]
    [Tooltip("Max distance (meters) hand target can be from shoulder. Measure upper arm + forearm length.")]
    public float maxArmReach = 0.55f;

    public float multipler;

    IMotionSource source;

    // Cached: source chest position/rotation at first valid frame,
    // used to compute offsets if characters are at different world positions.
    bool initialized;
    Vector3 srcChestStartPos;
    Vector3 localChestStartPos;

    bool _rightHandBaselineSet;
    Vector3 _rightHandOrigin;

    public bool needClamping;
    public bool needToUseRelative;

    public Vector3 _wristAnchor;
    public Transform _rightHandAnchor;
    public Transform _leftHandAnchor;

    [Header("Pull target toward wrist")]
    [Range(0f, 1f)]
    public float wristPull = 0.5f;   // 0 = pure data, 1 = glued to wrist

    public Transform _rightHand;

    public bool rightHandOnly;
    public bool leftHandOnly;
    public bool chestOnly;
    public bool allMovementData;

    [Header("Sensor Smoothing")]
    public float chestPositionThreshold = 0.02f;
    public float chestSmoothSpeed = 10f;

    public Vector3 globalcoordLeft;
    public Vector3 globalcoordRight;
    public Vector3 globalcoordChest;

    public bool GizmosDistForRight;
    public bool GizmosDistForLeft;
    public bool GizmosNameAndCoord;

    public static bool toggleGizmosDistForRight;
    public static bool toggleGizmosDistForLeft;
    public static bool toogleGizmosNameAndCoord;

    // for Drifting issue
    bool calibrated;
    Vector3 startAvatarPos;
    Quaternion startAvatarRot;
    Vector3 startChestPos;
    Quaternion startChestRot;



    void Start()
    {
        source = sourceObject as IMotionSource;
        if (source == null)
            Debug.LogError("RigReceiver: sourceObject does not implement IMotionSource!", this);

        localChestRef = animator.GetBoneTransform(HumanBodyBones.Chest);
        _wristAnchor = animator.GetBoneTransform(HumanBodyBones.RightHand).position; // wrist at rest
        _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        _rightHandAnchor = animator.GetBoneTransform(HumanBodyBones.RightHand);
        _leftHandAnchor = animator.GetBoneTransform(HumanBodyBones.LeftHand);

    }

    void Update()
    {
        if (source == null) return;
        // print(source.ChestPosition);

        //// Lazy init � wait until source has valid data
        //if (!initialized/* && source.ChestPosition != Vector3.zero*/) // Note: if source starts at (0,0,0) this will never initialize. Consider adding a "calibrate" button or similar.
        //{
        //    srcChestStartPos = source.ChestPosition;
        //    localChestStartPos = localChestRef != null ? localChestRef.position : transform.position;
        //    initialized = true;
        //}
        //if (!initialized) return;

        ////print("RigReceiver: applying source data to targets");
        //// Chest target � apply rotation directly, offset position
        ////Vector3 chestDelta = source.ChestPosition - srcChestStartPos;
        ////chestTarget.position = localChestStartPos + chestDelta;
        //if(!useChestBoneAsChestRef)
        //chestTarget.rotation = source.ChestRotation;
        //else
        //    localChestRef.rotation = source.ChestRotation;

        //// Hand targets � compute offset relative to source chest, apply to local chest
        //Vector3 srcChestPos = source.ChestPosition;
        //Quaternion srcChestRot = source.ChestRotation;
        //Vector3 leftOffset = Quaternion.Inverse(srcChestRot) * (source.LeftHandPosition - srcChestPos);
        //Vector3 rightOffset = Quaternion.Inverse(srcChestRot) * (source.RightHandPosition - srcChestPos);

        //Vector3 localChestPos = chestTarget.position;
        //Quaternion localChestRot = chestTarget.rotation;
        //leftHandTarget.position = source.LeftHandPosition;
        ////leftHandTarget.position = ClampToReach(
        ////    localChestPos + localChestRot * leftOffset,
        ////    leftShoulderBone != null ? source.LeftHandPosition : localChestPos,
        ////    maxArmReach);
        //leftHandTarget.rotation = source.LeftHandRotation;
        //rightHandTarget.position = source.RightHandPosition;
        ////rightHandTarget.position = ClampToReach(
        ////    localChestPos + localChestRot * rightOffset,
        ////    rightShoulderBone != null ? rightShoulderBone.position : localChestPos,
        ////    maxArmReach);
        //rightHandTarget.rotation = source.RightHandRotation;
        // --- Chest drives everything ---
        //chestTarget.rotation = source.ChestRotation;
        if (rightHandOnly)
        {
            if (needClamping)
            {
                Vector3 rawRight = source.RightHandPosition * multipler;
                Vector3 handPos = animator.GetBoneTransform(HumanBodyBones.RightHand).position;

                // Move target toward where the hand actually is, by wristPull amount
                //  rightHandTarget.position = Vector3.Lerp(rawRight, handPos, wristPull);
                rightHandTarget.rotation = source.RightHandRotation;
            }
            else
            {
                //rightHandTarget.position = (source.RightHandPosition) * multipler;
                //rightHandTarget.rotation = source.RightHandRotation;
                //rightHandTarget.SetPositionAndRotation(source.RightHandPosition, source.RightHandRotation);
                rightHandTarget.rotation = source.RightHandRotation;

                print("rightinRig " + rightHandTarget.position);

            }

        }
        //         else if (allMovementData)
        //         {
        //             chestTarget.rotation = source.ChestRotation;
        //             // transform.position = source.ChestPosition;

        //             // Smoothly move the root position toward the sensor data to filter out jitter and look natural
        //           // ApplyChestRotationWithBodyTurn();

        // // for  drifting issue
        // if (!calibrated)
        // {
        //     startAvatarPos = transform.position;
        //     startAvatarRot = transform.rotation;

        //     startChestPos = source.ChestPosition;
        //     startChestRot = source.ChestRotation;

        //     calibrated = true;
        // }

        // // Position relative to start
        // Vector3 deltaPos = source.ChestPosition - startChestPos;
        // deltaPos.y = 0f;

        // transform.position = Vector3.Lerp(
        //     transform.position,
        //     startAvatarPos + deltaPos,
        //     Time.deltaTime * chestSmoothSpeed
        // );

        // // Rotation relative to start
        // Quaternion deltaRot = source.ChestRotation * Quaternion.Inverse(startChestRot);
        // transform.rotation = startAvatarRot * Quaternion.Euler(0f, deltaRot.eulerAngles.y, 0f);

        // // Chest rotation
        // chestTarget.rotation = source.ChestRotation;


        //     Vector3 newChestPosXZ = new Vector3(source.ChestPosition.x, 0, source.ChestPosition.z);
        //     if (Vector3.Distance(transform.position, newChestPosXZ) > chestPositionThreshold)
        //     {
        //         transform.position = Vector3.Lerp(
        //             transform.position,
        //             newChestPosXZ,
        //             Time.deltaTime * chestSmoothSpeed
        //         );
        //     }

        //             // Turn character if chest rotates more than 90 degrees
        //             // Vector3 chestForward = source.ChestRotation * Vector3.forward;
        //             // chestForward.y = 0;
        //             // if (chestForward.sqrMagnitude > 0.001f)
        //             // {
        //             //     chestForward.Normalize();      
        //             //     float angle = Vector3.SignedAngle(transform.forward, chestForward, Vector3.up);
        //             //     if (Mathf.Abs(angle) > 90f)
        //             //     {
        //             //         float excess = angle > 0 ? angle - 90f : angle + 90f;
        //             //         transform.Rotate(0, excess, 0);
        //             //     }
        //             // }

        //             if (needClamping)
        //             {
        //                 Vector3 rawRight = source.RightHandPosition * multipler;
        //                 Vector3 handPos = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
        //                 Vector3 rawLeft = source.LeftHandPosition * multipler;
        //                 Vector3 handPosleft = animator.GetBoneTransform(HumanBodyBones.LeftHand).position;



        //                 // Move target toward where the hand actually is, by wristPull amount
        //                 rightHandTarget.position = Vector3.Lerp(rawRight, handPos, wristPull);
        //                 rightHandTarget.rotation = source.RightHandRotation;



        //                 // Move target toward where the hand actually is, by wristPull amount
        //                 leftHandTarget.position = Vector3.Lerp(rawLeft, handPosleft, wristPull);
        //                 leftHandTarget.rotation = source.LeftHandRotation;
        //             }
        //             else
        //             {
        //                 //rightHandTarget.position = (source.RightHandPosition) * multipler;
        //                 //rightHandTarget.rotation = source.RightHandRotation;
        //                 rightHandTarget.SetPositionAndRotation(source.RightHandPosition, source.RightHandRotation);
        //                 print("rightinRig " + rightHandTarget.position);

        //                 leftHandTarget.SetPositionAndRotation(source.LeftHandPosition, source.LeftHandRotation);
        //                 print("rightinRig " + leftHandTarget.position);

        //             }
        //       }


        else if (allMovementData)
        {
            if (!calibrated)
            {
               // startAvatarPos = transform.position;
                startAvatarRot = transform.rotation;

               // startChestPos = source.ChestPosition;
                startChestRot = source.ChestRotation;

                calibrated = true;
            }

           // Vector3 deltaPos = source.ChestPosition - startChestPos;
            //deltaPos.y = 0f;

          //  transform.position = Vector3.Lerp(
            //    transform.position,
            //    startAvatarPos + deltaPos,
            //    Time.deltaTime * chestSmoothSpeed
           // );

            Quaternion deltaRot = source.ChestRotation * Quaternion.Inverse(startChestRot);

            Vector3 forward = deltaRot * Vector3.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude > 0.01f)
            {
                Quaternion yawOnly = Quaternion.LookRotation(forward.normalized, Vector3.up);
                transform.rotation = startAvatarRot * yawOnly;
            }

            chestTarget.rotation = source.ChestRotation;

            rightHandTarget.SetPositionAndRotation(source.RightHandPosition, source.RightHandRotation);
            leftHandTarget.SetPositionAndRotation(source.LeftHandPosition, source.LeftHandRotation);
        }
        else if (leftHandOnly)
        {
            if (needClamping)
            {
                Vector3 rawLeft = source.LeftHandPosition * multipler;
                Vector3 handPos = animator.GetBoneTransform(HumanBodyBones.LeftHand).position;

                // Move target toward where the hand actually is, by wristPull amount
                leftHandTarget.position = Vector3.Lerp(rawLeft, handPos, wristPull);
                leftHandTarget.rotation = source.LeftHandRotation;
            }
            else
            {
                //rightHandTarget.position = (source.RightHandPosition) * multipler;
                //rightHandTarget.rotation = source.RightHandRotation;
                leftHandTarget.SetPositionAndRotation(source.LeftHandPosition, source.LeftHandRotation);
                print("rightinRig " + leftHandTarget.position);

            }
        }
        else
        {
            chestTarget.rotation = source.ChestRotation;
            //  transform.position = source.ChestPosition;
        }
        globalcoordLeft = leftHandTarget.position;
        globalcoordRight = rightHandTarget.position;
        globalcoordChest = chestTarget.position;



        //if(needToUseRelative)
        //{
        //    Vector3 rawRight = source.RightHandPosition;

        //    if (!_rightHandBaselineSet && IMUDataSource.firstTime)
        //    {
        //        _rightHandOrigin = rawRight;
        //        // Anchor to where the hand bone actually is right now
        //        _rightHandAnchor = animator.GetBoneTransform(HumanBodyBones.RightHand);
        //        _rightHandBaselineSet = true;
        //        print("right " + rawRight);
        //        _rightHandOrigin = rawRight;
        //       // rightHandTarget.position = rawRight;
        //        _rightHandBaselineSet = true;
        //    }
        //    if (IMUDataSource.firstTime)
        //    {
        //        var localRightPos = rightHandTarget.position;
        //        rightHandTarget.position = rawRight;
        //       // rightHandTarget.position = rawRight -_rightHandAnchor.position;
        //        //rightHandTarget.rotation = source.RightHandRotation;


        //        print("rightFiler " + rightHandTarget.position + rawRight);
        //    }
        //}
        //else
        //{
        //    if (needClamping)
        //    {
        //        //Vector3 chestPos = chestTarget.position;
        //        ////Quaternion chestRot = chestTarget.rotation;
        //        //// LEFT HAND
        //        //Vector3 leftOffset = source.LeftHandPosition - source.ChestPosition;
        //        //leftOffset = Vector3.ClampMagnitude(leftOffset, maxArmReach);
        //        //leftHandTarget.position = chestPos + leftOffset;
        //        //leftHandTarget.rotation = source.LeftHandRotation;
        //        // RIGHT HAND
        //        Vector3 rightOffset = (-source.RightHandPosition) + chestTarget.position  /*- new Vector3(0,1.45f,0)*/;
        //        rightOffset = Vector3.ClampMagnitude(rightOffset, maxArmReach);
        //        rightHandTarget.position = /*chestPos + */rightOffset;


        //    }
        //    else
        //    {
        //        print("rightinRig " + rightHandTarget.position);
        //        rightHandTarget.position = (source.RightHandPosition) * multipler;
        //        rightHandTarget.rotation = source.RightHandRotation;

        //    }
        //}



        //rightHandTarget.rotation = source.RightHandRotation;

        //leftHandTarget.position = (source.LeftHandPosition) * multipler;
        //leftHandTarget.rotation = source.LeftHandRotation;
        //Debug.Log($"Right : {rightHandTarget.position:F3},{rightHandTarget.localPosition:F3}");
        //Debug.Log($"Right Interface RightHandPos: {source.RightHandPosition.x:F3}, {source.RightHandPosition.y:F3}, {source.RightHandPosition.z:F3}");
    }



    public float maxChestYaw = 90f;
    public float bodyTurnSmoothSpeed = 6f;

    void ApplyChestRotationWithBodyTurn()
    {
        Quaternion sourceChestRot = source.ChestRotation;

        Vector3 chestForward = sourceChestRot * Vector3.forward;
        chestForward.y = 0f;

        if (chestForward.sqrMagnitude < 0.001f)
            return;

        chestForward.Normalize();

        float yawAngle = Vector3.SignedAngle(transform.forward, chestForward, Vector3.up);

        float chestYaw = Mathf.Clamp(yawAngle, -maxChestYaw, maxChestYaw);

        float bodyTurnAmount = 0f;

        if (Mathf.Abs(yawAngle) > maxChestYaw)
        {
            float excessYaw = yawAngle - chestYaw;

            bodyTurnAmount = Mathf.Lerp(
                0f,
                excessYaw,
                Time.deltaTime * bodyTurnSmoothSpeed
            );

            transform.Rotate(0f, bodyTurnAmount, 0f, Space.World);
        }

        Quaternion bodyRot = transform.rotation;

        Quaternion localChestRot = Quaternion.Inverse(bodyRot) * sourceChestRot;
        Vector3 localEuler = localChestRot.eulerAngles;

        localEuler.y = chestYaw;

        Quaternion correctedChestRot = bodyRot * Quaternion.Euler(localEuler);

        chestTarget.rotation = Quaternion.Slerp(
            chestTarget.rotation,
            correctedChestRot,
            Time.deltaTime * chestSmoothSpeed
        );

        print("Yaw: " + yawAngle + " | ChestYaw: " + chestYaw + " | BodyTurn: " + bodyTurnAmount);
    }


    private void OnDrawGizmos()
    {
        if (GizmosDistForRight)
            toggleGizmosDistForRight = true;
        else
            toggleGizmosDistForRight = false;

        if (GizmosDistForLeft)
            toggleGizmosDistForLeft = true;
        else
            toggleGizmosDistForLeft = false;
        if (GizmosNameAndCoord)
            toogleGizmosNameAndCoord = true;
        else
            toogleGizmosNameAndCoord = false;
    }

    // private void FixedUpdate()
    // {

    // }

    void OnAnimatorIK(int layerIndex)
    {
        if (source == null) return;
        Quaternion srcChestRot = source.ChestRotation;

    }
    /// Clamps a target position so it never exceeds maxDist from the origin (shoulder).
    /// Direction is preserved — the arm points toward the target but stops at full extension.
    Vector3 ClampToReach(Vector3 targetPos, Vector3 shoulderPos, float maxDist)
    {
        Vector3 offset = targetPos - shoulderPos;
        float dist = offset.magnitude;
        if (dist > maxDist && dist > 0.001f)
        {
            return shoulderPos + (offset / dist) * maxDist;
        }
        return targetPos;
    }
}