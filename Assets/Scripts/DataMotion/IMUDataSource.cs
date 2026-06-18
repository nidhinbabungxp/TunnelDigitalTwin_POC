using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;


    /// <summary>
    /// Concrete <see cref="IMotionSource"/> that polls an HTTP endpoint for IMU
    /// sensor readings (CHEST, LEFT_HAND, RIGHT_HAND) and exposes them as
    /// world-space positions and rotations.
    ///
    /// Data flow:  HTTP JSON  ->  ParseResponse  ->  ApplyReading  ->  IMotionSource properties.
    /// The coordinate-frame mapping (axis swaps / sign flips) is applied here,
    /// at the data source, so receivers can consume the values directly.
    /// </summary>
    public class IMUDataSource : MonoBehaviour, IMotionSource
    {
        // -----------------------------------------------------------------
        // Inspector configuration
        // -----------------------------------------------------------------
        public Quaternion rianRotation = Quaternion.identity;

        [Header("Combined Mode — one URL returns all sensors")]
        [Tooltip("URL returning a JSON array [{...},{...},{...}] or single object {...}")]
        public string combinedUrl = "http://192.168.1.100:8080/sensors";

        [Header("Polling")]
        [Tooltip("Polling interval in seconds (0.05 = 20Hz)")]
        public float pollInterval = 0.05f;

        [Header("Sensor Location Names (must match JSON 'location' field)")]
        public string chestName = "CHEST";
        public string leftName = "LEFT_HAND";
        public string rightName = "RIGHT_HAND";

        [Header("Euler Convention — adjust if rotation is wrong")]
        public Vector3 chestEulerOffset = Vector3.zero;
        public Vector3 leftHandEulerOffset = Vector3.zero;
        public Vector3 rightHandEulerOffset = Vector3.zero;

        [Header("Position Estimation (IMU has no position)")]
        [Tooltip("Approximate arm length in meters")]
        public float armLength = 0.55f;

        [Header("Debug")]
        public bool logData = false;
        public bool chestOffset = false;

        // -----------------------------------------------------------------
        // IMotionSource — world-space outputs consumed by receivers
        // -----------------------------------------------------------------
        public Quaternion ChestRotation { get; private set; } = Quaternion.identity;
        public Quaternion LeftHandRotation { get; private set; } = Quaternion.identity;
        public Quaternion RightHandRotation { get; private set; } = Quaternion.identity;

        public Vector3 ChestPosition { get; private set; } = Vector3.zero;
        public Vector3 LeftHandPosition { get; private set; } = Vector3.zero;
        public Vector3 RightHandPosition { get; private set; } = Vector3.zero;

        // -----------------------------------------------------------------
        // Debug visualisation + calibration
        // -----------------------------------------------------------------

        // Set true once the first right-hand reading has been received
        // (consumed by RigReceiver's relative-baseline logic).
        public static bool firstTime;

        public bool calibrate;

        // Rotation-zeroing state: _inverseQt is the captured "zero" pose,
        // _rawQt is the most recent raw chest quaternion.
        Quaternion _inverseQt = Quaternion.identity;
        Quaternion _rawQt;

        // -----------------------------------------------------------------
        // Unity lifecycle
        // -----------------------------------------------------------------

        /// <summary>Starts the polling loop and resets the rotation-zero offset.</summary>
        void Start()
        {
            StartCoroutine(PollLoop());
            _inverseQt = Quaternion.identity;
        }

        /// <summary>Repeatedly fetches the combined sensor URL at <see cref="pollInterval"/>.</summary>
        IEnumerator PollLoop()
        {
            while (true)
            {
                yield return StartCoroutine(FetchUrl(combinedUrl));
                yield return new WaitForSeconds(pollInterval);
            }
        }

        // -----------------------------------------------------------------
        // Fetch + Parse
        // -----------------------------------------------------------------

        /// <summary>Performs a single HTTP GET and forwards the body to the parser.</summary>
        IEnumerator FetchUrl(string url)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 2;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (logData) Debug.LogWarning($"IMUDataSource: {url} → {req.error}");
                    yield break;
                }

                if (logData)
                    print("Received IMU data: " + req.downloadHandler.text);

                ParseResponse(req.downloadHandler.text);
            }
        }

        /// <summary>
        /// Deserialises the nested "activity" JSON format and applies each
        /// sensor reading (chest / left_hand / right_hand).
        /// </summary>
        void ParseResponse(string json)
        {
            string trimmed = json.Trim();

            // Format B: { "activity": { "chest": {...}, "left_hand": {...}, "right_hand": {...} } }
            ActivityResponse resp = JsonUtility.FromJson<ActivityResponse>(trimmed);
            if (resp != null && resp.activity != null && resp.activity.chest != null)
            {
                ApplyReading(resp.activity.chest, "chest");
                ApplyReading(resp.activity.left_hand, "left_hand");
                ApplyReading(resp.activity.right_hand, "right_hand");
            }
        }

        // -----------------------------------------------------------------
        // Apply a single sensor reading
        // -----------------------------------------------------------------

        /// <summary>
        /// Maps one <see cref="SensorReading"/> into the matching IMotionSource
        /// output, applying the coordinate-frame transform for that location.
        /// </summary>
        /// <param name="r">The parsed sensor reading.</param>
        /// <param name="chest">Optional location override used by the parser.</param>
        void ApplyReading(SensorReading r, string chest = null)
        {
            if (r == null) return;
            if (!string.IsNullOrEmpty(chest)) r.location = chest;

            if (string.Equals(r.location, chestName, StringComparison.OrdinalIgnoreCase))
            {
                // Chest: remap axes, then apply the captured zero offset every frame.
                Quaternion quaternion = new Quaternion(-r.rotation.qx, -r.rotation.qz, -r.rotation.qy, r.rotation.qw);

                if (chestOffset)
                    _rawQt = quaternion * _inverseQt;
                else
                    _rawQt = quaternion;
                ChestRotation = _rawQt ;

                ChestPosition = new Vector3(r.position_abs.x, 0, r.position_abs.y);

                if (logData) Debug.Log($"CHEST: {quaternion}");
            }
            else if (string.Equals(r.location, leftName, StringComparison.OrdinalIgnoreCase))
            {
                // Left hand: remap axes/signs for rotation, absolute position, and debug cube.
                Quaternion quaternion =new Quaternion(-r.rotation.qx, -r.rotation.qz, -r.rotation.qy, r.rotation.qw);
                LeftHandRotation = quaternion;

                LeftHandPosition = new Vector3(
                     r.position_abs.x,
                     r.position_abs.z,
                     r.position_abs.y);

                Debug.Log($"Left Interface leftHandPos: {LeftHandPosition.x:F3}, {LeftHandPosition.y:F3}, {LeftHandPosition.z:F3}");

                if (logData) Debug.Log($"LEFT: {quaternion}");
            }
            else if (string.Equals(r.location, rightName, StringComparison.OrdinalIgnoreCase))
            {
                // Right hand: rotation kept in native axis order; position/cube remapped.
                Quaternion quaternion = new Quaternion(-r.rotation.qx, -r.rotation.qz, -r.rotation.qy, r.rotation.qw);
                RightHandRotation = quaternion;

                RightHandPosition = new Vector3(
                     r.position_abs.x,
                     r.position_abs.z,
                     r.position_abs.y);

                Debug.Log($"Right Interface RightHandPos: {RightHandPosition.x:F3}, {RightHandPosition.y:F3}, {RightHandPosition.z:F3}");

                if (logData) Debug.Log($"RIGHT: {quaternion}");

                firstTime = true;
            }
        }

        /// <summary>
        /// Captures the current raw chest orientation as the zero/upright pose.
        /// Call once while the sensor is held in the reference pose.
        /// </summary>
        public void ResetRotation()
        {
            _inverseQt = Quaternion.Inverse(_rawQt);
        }

        // -----------------------------------------------------------------
        // JSON data classes
        // -----------------------------------------------------------------

        /// <summary>Top-level wrapper: { "activity": {...} }.</summary>
        [Serializable]
        public class ActivityResponse
        {
            public ActivityData activity;
        }

        /// <summary>The three per-frame sensor readings.</summary>
        [Serializable]
        public class ActivityData
        {
            public SensorReading chest;
            public SensorReading left_hand;
            public SensorReading right_hand;
        }

        /// <summary>Relative position component of a reading.</summary>
        [Serializable]
        public class PositionData
        {
            public float x;
            public float y;
            public float z;
        }

        /// <summary>Quaternion rotation component of a reading.</summary>
        [Serializable]
        public class RotationData
        {
            public float qw;
            public float qx;
            public float qy;
            public float qz;
        }

        /// <summary>Absolute (world) position component of a reading.</summary>
        [Serializable]
        public class AbsRotationData
        {
            public float x;
            public float y;
            public float z;
        }

        /// <summary>One sensor reading: location label, positions, and rotation.</summary>
        [Serializable]
        public class SensorReading
        {
            public string location;
            public PositionData position;
            public AbsRotationData position_abs;
            public RotationData rotation;
        }

        /// <summary>Alternative flat IMU payload with validation helper (currently unused).</summary>
        public class IMUSensorData
        {
            // Required
            public float qw, qx, qy, qz;
            public string state;
            public string location;

            // Optional with safe defaults
            public float ax = 0, ay = 0, az = 0;
            public string gesture = "NONE";

            /// <summary>True when the quaternion and state are populated.</summary>
            public bool IsValid() => !float.IsNaN(qw) && !string.IsNullOrEmpty(state);
        }

        /// <summary>Helper for deserialising a bare JSON array via JsonUtility.</summary>
        public static class JsonArrayHelper
        {
            public static T[] FromJson<T>(string json)
            {
                string wrapped = "{\"items\":" + json + "}";
                Wrapper<T> w = JsonUtility.FromJson<Wrapper<T>>(wrapped);
                return w.items;
            }

            [Serializable]
            private class Wrapper<T>
            {
                public T[] items;
            }
        }
    }

