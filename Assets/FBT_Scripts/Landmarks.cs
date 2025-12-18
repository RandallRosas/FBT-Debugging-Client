using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization;

/*
    Handles full body pose tracking using MediaPipe data.
    Uses a Particle System for visualizing the landmarks in Unity.
*/
public class BodyTracking : MonoBehaviour
{
    public UDPReceive udpReceive;
    public ParticleSystem poseParticles;
    
    [Header("Tracking Configuration")]
    [Tooltip("Root GameObject transform, particles will be positioned relative to this.")]
    public Transform rootTransform;
    public float scale = 800.0f;
    public float xOffset = 2.0f;

    [Tooltip("Mirror X-axis to fix left/right swapping.")]
    public bool mirrorX = true;

    [Tooltip("Camera image width (should match Server.py camera width).")]
    public float cameraImageWidth = 1280f;
    
    [Header("VR Player Tracking")]
    [Tooltip("If true, positions will be relative to the rootTransform's position.")]
    public bool useRelativePositioning = true;
    
    [Header("Smoothing Settings")]
    [Range(0.0f, 1.0f)]
    [Tooltip("Higher values = more smoothing. Less jitter, but more lag.")]
    public float smoothingFactor = 0.7f;
    
    [Tooltip("Enable velocity-based filtering for additional stability.")]
    public bool useVelocityFilter = true;
    
    [Range(0.0f, 10.0f)]
    [Tooltip("Maximum allowed movement per frame.")]
    public float maxMovementPerFrame = 2.0f;
    
    [Header("Depth (Z-axis) Smoothing")]
    [Tooltip("Apply extra smoothing to just the Z-axis (depth).")]
    public bool useExtraDepthSmoothing = true;
    
    [Range(0.0f, 1.0f)]
    [Tooltip("Z-axis smoothing factor. Higher = smoother depth, but more lag.")]
    public float depthSmoothingFactor = 0.9f;

    [Range(0.0f, 1.0f)]
    [Tooltip("Minimum Z-axis change threshold. Used to ignore tiny depth changes to reduce jitter.")]
    public float depthChangeThreshold = 0.01f;
    
    private ParticleSystem.Particle[] particles;
    private Vector3[] smoothedPositions;
    private Vector3[] previousPositions;
    private float[] smoothedZValues;
    private bool[] isInitialized;
    private const int NUM_LANDMARKS = 33;
    private Vector3[] rawWorldPositions;
    private Vector3 calibrationOffset;
    private Vector3 calibratedBodyCenterLocal;
    private bool isCalibrated = false;
    
    // MediaPipe Pose landmark indices.
    private const int LEFT_SHOULDER = 11;
    private const int RIGHT_SHOULDER = 12;
    private const int LEFT_ELBOW = 13;
    private const int RIGHT_ELBOW = 14;
    private const int LEFT_WRIST = 15;
    private const int RIGHT_WRIST = 16;
    private const int LEFT_HIP = 23;
    private const int RIGHT_HIP = 24;

    // Initialize the particle system and smoothing.
    void Start()
    {
        if (rootTransform == null)
        {
            rootTransform = transform;
        }
        
        if (rootTransform == transform)
        {
            rootTransform.localRotation = Quaternion.identity;
        }
        
        if (poseParticles == null)
        {
            poseParticles = GetComponent<ParticleSystem>();
            if (poseParticles == null)
            {
                Debug.LogError("ParticleSystem not assigned! Please assign a ParticleSystem component.");
                return;
            }
        }
        
        // Ensure particle system is a child of root transform for local space positioning.
        if (poseParticles.transform.parent != rootTransform && poseParticles.transform != rootTransform)
        {
            poseParticles.transform.SetParent(rootTransform, false);
            poseParticles.transform.localRotation = Quaternion.identity;
            poseParticles.transform.localScale = Vector3.one;
        }
        else if (poseParticles.transform == rootTransform)
        {
            // If particle system IS the root, make sure it has identity rotation.
            poseParticles.transform.localRotation = Quaternion.identity;
        }
        else
        {
            // Already a child, but make sure rotation is correct.
            poseParticles.transform.localRotation = Quaternion.identity;
            poseParticles.transform.localScale = Vector3.one;
        }

        // Particle system configuration.
        var main = poseParticles.main;
        main.maxParticles = NUM_LANDMARKS;
        main.startLifetime = Mathf.Infinity;
        main.startSize = 0.1f;
        main.startColor = Color.red;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = poseParticles.emission;
        emission.enabled = true;

        // Initialize particle array.
        particles = new ParticleSystem.Particle[NUM_LANDMARKS];
        
        // Pre-populate particles. They'll be positioned in Update.
        for (int i = 0; i < NUM_LANDMARKS; i++)
        {
            particles[i].position = Vector3.zero;
            particles[i].startLifetime = Mathf.Infinity;
            particles[i].remainingLifetime = Mathf.Infinity;
            particles[i].startSize = main.startSize.constant;
            particles[i].startColor = main.startColor.color;
        }

        // Set initial particles.
        poseParticles.SetParticles(particles, NUM_LANDMARKS);
        
        // Initialize smoothing arrays.
        smoothedPositions = new Vector3[NUM_LANDMARKS];
        previousPositions = new Vector3[NUM_LANDMARKS];
        smoothedZValues = new float[NUM_LANDMARKS];
        isInitialized = new bool[NUM_LANDMARKS];
        
        // Initialize reusable arrays to avoid allocations every frame.
        rawWorldPositions = new Vector3[NUM_LANDMARKS];
    }

    /*
        Update particle positions with MediaPipe pose data.
        
        Parses UDP data, applies smoothing, and updates particle positions.
    */
    void Update() {
        if (poseParticles == null || particles == null || rootTransform == null) return;

        string data = udpReceive.data;
        if (string.IsNullOrEmpty(data)) return;

        // Need to remove these brackets sent by Python's str(list).
        string cleanedData = data.Replace("[", "").Replace("]", "");
        
        // Split the data into individual landmark coordinates.
        string[] points = cleanedData.Split(',');
        if (points.Length < NUM_LANDMARKS * 3) return;
        
        // Parse all positions into a reusable array.
        for (int i = 0; i < NUM_LANDMARKS; i++) {
            float pX = float.Parse(points[i * 3], CultureInfo.InvariantCulture);
            float pY = float.Parse(points[i * 3 + 1], CultureInfo.InvariantCulture);
            float pZ = float.Parse(points[i * 3 + 2], CultureInfo.InvariantCulture);

            // Apply X-axis mirroring if enabled. This fixes left/right swap from camera perspective.
            if (mirrorX)
            {
                pX = cameraImageWidth - pX;
            }

            float x = xOffset - (pX / scale);
            float y = pY / scale;
            float z = pZ / scale;

            rawWorldPositions[i] = new Vector3(x, y, z);
        }

        // Calculate the current body center for the dynamic offset calculation.
        Vector3 currentBodyCenter = (rawWorldPositions[LEFT_HIP] + rawWorldPositions[RIGHT_HIP]) * 0.5f;
        Vector3 currentOffset = Vector3.zero;
        
        if (useRelativePositioning && isCalibrated)
        {
            // Calculate the offset to keep the body center aligned with the rootTransform.
            // This updates every frame so particles follow the player when they move.
            Vector3 targetBodyCenterWorld = rootTransform.TransformPoint(calibratedBodyCenterLocal);
            currentOffset = targetBodyCenterWorld - currentBodyCenter;
        }
        
        // Update the particle positions for each landmark.
        for (int i = 0; i < NUM_LANDMARKS; i++) {
            Vector3 newPosition = rawWorldPositions[i];
            
            // Apply the calibration offset to make the positions relative to the player.
            if (useRelativePositioning && isCalibrated)
            {
                newPosition = newPosition + currentOffset;
            }
            
            Vector3 localPosition = rootTransform.InverseTransformPoint(newPosition);
            
            // Apply extra smoothing to Z-axis (depth) if enabled.
            if (useExtraDepthSmoothing)
            {
                if (!isInitialized[i])
                {
                    smoothedZValues[i] = localPosition.z;
                }
                else
                {
                    // Check if the Z change is significant enough to update.
                    float zChange = Mathf.Abs(localPosition.z - smoothedZValues[i]);
                    if (zChange > depthChangeThreshold)
                    {
                        // Apply aggressive smoothing to the Z-axis.
                        smoothedZValues[i] = Mathf.Lerp(smoothedZValues[i], localPosition.z, 1.0f - depthSmoothingFactor);
                    }
                }
                // Use the smoothed Z value.
                localPosition.z = smoothedZValues[i];
            }
            
            // Apply smoothing to the X and Y axes.
            if (!isInitialized[i])
            {
                smoothedPositions[i] = localPosition;
                isInitialized[i] = true;
            }
            else
            {
                // Velocity-based filtering.
                if (useVelocityFilter)
                {
                    Vector3 velocity = localPosition - previousPositions[i];
                    float distance = velocity.magnitude;
                    
                    // Clamp the sudden movements.
                    if (distance > maxMovementPerFrame)
                    {
                        localPosition = previousPositions[i] + velocity.normalized * maxMovementPerFrame;
                    }
                }
                
                // Lerp smoothing to preserve the smoothed Z value.
                Vector3 smoothedXY = Vector3.Lerp(
                    new Vector3(smoothedPositions[i].x, smoothedPositions[i].y, 0),
                    new Vector3(localPosition.x, localPosition.y, 0),
                    1.0f - smoothingFactor
                );
                smoothedPositions[i] = new Vector3(smoothedXY.x, smoothedXY.y, localPosition.z);
            }
            
            previousPositions[i] = localPosition;
            
            /*
                The particles are in local space relative to the particle system's transform.
                Since the particle system is parented to the root in Start(), the positions are already in the correct local space.
                Update the particle position with the smoothed value (in local space).
            */
            particles[i].position = smoothedPositions[i];
        }

        // Apply all particle updates at once.
        poseParticles.SetParticles(particles, NUM_LANDMARKS);
    }
    
    public Vector3[] GetSmoothedPositions()
    {
        return smoothedPositions;
    }
    
    public int GetNumLandmarks()
    {
        return NUM_LANDMARKS;
    }
    
    public Transform GetRootTransform()
    {
        return rootTransform;
    }
    
    /*
        Calibrate the tracking from a UI button.
        Uses the current pose data to calibrate.
    */
    public void CalibrateNow()
    {
        if (rawWorldPositions == null || rawWorldPositions.Length < NUM_LANDMARKS)
        {
            Debug.LogWarning("Can't calibrate, no pose data available yet.");
            return;
        }
        CalibratePosition(rawWorldPositions);
    }
    
    // Resets calibration.
    public void ResetCalibration()
    {
        isCalibrated = false;
        calibrationOffset = Vector3.zero;
        calibratedBodyCenterLocal = Vector3.zero;
    }
    
    /*
        Calibrates the tracking position based on current pose.
        Uses the midpoint between hips as the calibration point.
    */
    private void CalibratePosition(Vector3[] positions)
    {
        if (rootTransform == null) return;
        
        // Use the midpoint between hips as the body center for calibration.
        Vector3 leftHip = positions[LEFT_HIP];
        Vector3 rightHip = positions[RIGHT_HIP];
        Vector3 bodyCenter = (leftHip + rightHip) * 0.5f;
        
        calibrationOffset = rootTransform.position - bodyCenter;
        
        calibratedBodyCenterLocal = rootTransform.InverseTransformPoint(bodyCenter + calibrationOffset);
        
        isCalibrated = true;
    }
    
    public bool IsCalibrated()
    {
        return isCalibrated;
    }
}
