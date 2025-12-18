using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
    Renders skeleton lines connecting MediaPipe pose landmarks.
    Creates LineRenderer components to visualize the pose skeleton structure.
*/
public class LineCode : MonoBehaviour
{
    [Header("Skeleton Visualization")]
    [Tooltip("Reference to the BodyTracking component to get the pose data.")]
    public BodyTracking bodyTracking;
    
    [Tooltip("Enable the skeleton line rendering.")]
    public bool showSkeleton = true;
    
    [Tooltip("Line width for the skeleton.")]
    public float lineWidth = 0.1f;
    
    [Tooltip("Color of the skeleton lines.")]
    public Color lineColor = Color.green;

  
    private List<LineRenderer> skeletonLineRenderers;
    private Material sharedLineMaterial;
    
    // MediaPipe Pose connections (pairs of landmark indices that should be connected).
    private static readonly int[,] POSE_CONNECTIONS = new int[,]
    {
        // Face
        {8, 6}, {6, 5}, {5, 4}, {4, 0}, {0, 1}, {1, 2}, {2, 3}, {3, 7}, {10, 9},
        // Torso
        {12, 11}, {11, 23}, {23, 24}, {24, 12},
        // Left arm
        {11, 13}, {13, 15}, {15, 17}, {15, 19}, {15, 21}, {17, 19},
        // Right arm
        {12, 14}, {14, 16}, {16, 18}, {16, 20}, {16, 22}, {18, 20},
        // Left leg
        {23, 25}, {25, 27}, {27, 29}, {27, 31}, {29, 31},
        // Right leg
        {24, 26}, {26, 28}, {28, 30}, {28, 32}, {30, 32}
    };

    // Initialize the skeleton line renderers.
    void Start()
    {
        // Try to find the BodyTracking component if not assigned.
        if (bodyTracking == null)
        {
            bodyTracking = FindFirstObjectByType<BodyTracking>();
            if (bodyTracking == null)
            {
                Debug.LogError("Please assign the BodyTracking component in the Inspector.");
                return;
            }
        }
        
        // Parent this GameObject to the root transform so the local positions work correctly.
        Transform rootTransform = bodyTracking.GetRootTransform();
        if (rootTransform != null && transform.parent != rootTransform && transform != rootTransform)
        {
            transform.SetParent(rootTransform, false);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
        else if (transform == rootTransform)
        {
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
        
        if (showSkeleton)
        {
            InitializeSkeletonLines();
        }
    }
    
    // Initialize the skeleton line renderers.
    private void InitializeSkeletonLines()
    {
        // Clear the existing line renderers.
        if (skeletonLineRenderers != null)
        {
            foreach (var lr in skeletonLineRenderers)
            {
                if (lr != null && lr.gameObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(lr.gameObject);
                    else
                        DestroyImmediate(lr.gameObject);
                }
            }
        }
        
        skeletonLineRenderers = new List<LineRenderer>();
        
        // Get the root transform to ensure the lines are in the same coordinate space as the particles.
        Transform rootTransform = bodyTracking.GetRootTransform();

        // Using the shared material to avoid memory leaks.
        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null)
        {
            sharedLineMaterial = new Material(lineShader);
        }
        else
        {
            // Fallback shader.
            sharedLineMaterial = new Material(Shader.Find("Unlit/Color"));
        }
        
        // Create a LineRenderer for each connection.
        int numConnections = POSE_CONNECTIONS.GetLength(0);
        for (int i = 0; i < numConnections; i++)
        {
            GameObject lineObj = new GameObject($"SkeletonLine_{i}");
            lineObj.transform.SetParent(rootTransform, false);
            lineObj.transform.localPosition = Vector3.zero;
            lineObj.transform.localRotation = Quaternion.identity;
            lineObj.transform.localScale = Vector3.one;
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.material = sharedLineMaterial;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            skeletonLineRenderers.Add(lr);
        }
        
    }
    
    // Clean up the line renderers and shared material when thecomponent is destroyed.
    private void OnDestroy()
    {
        if (skeletonLineRenderers != null)
        {
            foreach (var lr in skeletonLineRenderers)
            {
                if (lr != null && lr.gameObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(lr.gameObject);
                    else
                        DestroyImmediate(lr.gameObject);
                }
            }
            skeletonLineRenderers.Clear();
        }
        
        if (sharedLineMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(sharedLineMaterial);
            else
                DestroyImmediate(sharedLineMaterial);
            sharedLineMaterial = null;
        }
    }

    // Update skeleton line positions each frame.
    void Update()
    {
        if (!showSkeleton || bodyTracking == null || skeletonLineRenderers == null) return;
        
        UpdateSkeletonLines();
    }
    
    /*
        Update the skeleton line positions based on current pose data.
        Only draws lines if both landmarks are valid.
    */
    private void UpdateSkeletonLines()
    {
        Vector3[] smoothedPositions = bodyTracking.GetSmoothedPositions();
        int numLandmarks = bodyTracking.GetNumLandmarks();
        
        if (smoothedPositions == null) return;
        
        for (int i = 0; i < POSE_CONNECTIONS.GetLength(0) && i < skeletonLineRenderers.Count; i++)
        {
            LineRenderer lr = skeletonLineRenderers[i];
            if (lr == null) continue;
            
            int startIdx = POSE_CONNECTIONS[i, 0];
            int endIdx = POSE_CONNECTIONS[i, 1];
            
            if (startIdx < numLandmarks && endIdx < numLandmarks &&
                smoothedPositions[startIdx] != Vector3.zero && 
                smoothedPositions[endIdx] != Vector3.zero)
            {
                lr.enabled = true;
                lr.SetPosition(0, smoothedPositions[startIdx]);
                lr.SetPosition(1, smoothedPositions[endIdx]);
            }
            else
            {
                lr.enabled = false;
            }
        }
    }
}
