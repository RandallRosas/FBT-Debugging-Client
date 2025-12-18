# FBT Debugging Client - Full Body Tracking Setup Guide

This project uses MediaPipe pose detection to track full body movements and retarget them to a Unity avatar using Meta's Character Retargeting system.

## Prerequisites

- **Python 3.8+** installed on your computer
- **Unity 2022.3 LTS or later** (tested with Unity 6000.2.6f2)
- **Meta Quest 3** (or compatible VR headset)
- **Webcam** connected to your computer
- **Meta XR Movement SDK** (included in project via Unity Package Manager)

## Python Setup

### 1. Install Python Dependencies

Open a terminal/command prompt and install the required packages:

```bash
pip install opencv-python mediapipe numpy
```

Or if you prefer using a virtual environment:

```bash
python -m venv venv
source venv/bin/activate  # On Windows: venv\Scripts\activate
pip install opencv-python mediapipe numpy
```

### 2. Configure the Python Script

Edit `Server.py` and update the following settings:

```python
# Use "127.0.0.1" or "localhost" when testing in Unity Editor
# Use your Quest's IP address when building for Quest
UDP_IP = "localhost"
```

**Finding your Quest's IP address:**
1. Put on your Quest headset
2. Go to Settings → Wi-Fi → Click on your connected network
3. Note the IP address shown

### 3. Run the Python Script

```bash
python Server.py
```

You should see a window displaying your webcam feed with pose landmarks drawn on it. Press 'q' to quit.

**Note:** The script will automatically detect and use your default camera (usually camera index 0). If you have multiple cameras, you may need to change `cap = cv2.VideoCapture(0)` to a different index.

## Unity Setup

### 1. Open the Project

1. Open Unity Hub
2. Click "Add" and select the `FBT-Debugging-Client` folder
3. Open the project in Unity

### 2. Configure the Scene

The project should already have the necessary components set up, but verify:

1. **BodyTracking Component:**
   - Find the GameObject with the `BodyTracking` component
   - Assign a `ParticleSystem` component (or it will try to find one automatically)
   - Assign the `UDPReceive` component to the `udpReceive` field
   - Set `rootTransform` to your VR player's root transform (e.g., OVRPlayerController)

2. **UDPReceive Component:**
   - Ensure `port` is set to `5052` (must match `Server.py`)
   - `printToConsole` can be enabled for debugging

3. **HybridBodyProvider Component:**
   - Add this component to a GameObject in your scene
   - Assign the `BodyTracking` component to the `mediaPipeTracker` field
   - Configure which body parts to overwrite:
     - `overwriteHips`: Overwrites hip position with MediaPipe data
     - `overwriteLegs`: Overwrites leg rotations with MediaPipe data
     - `overwriteFeet`: Overwrites foot/ankle positions and rotations

4. **Character Retargeting:**
   - Ensure your avatar (e.g., KyleRobot) has the Character Retargeting building block configured
   - The `HybridBodyProvider` should be automatically detected as the source data provider
   - If `MetaSourceDataprovider` is enabled, make sure to disable it.

### 3. Testing in Unity Editor

1. Make sure `Server.py` is running with `UDP_IP = "127.0.0.1"` or `"localhost"`
2. Press Play in Unity
3. You should see pose particles and skeleton lines (if enabled) following your movements

### 4. Building for Quest

1. **Update Python Script:**
   - Change `UDP_IP` in `Server.py` to your Quest's IP address
   - Make sure your computer and Quest are on the same Wi-Fi network

2. **Build Settings:**
   - File → Build Settings
   - Select Android platform
   - Switch Platform if needed
   - Click "Build and Run" or "Build"

3. **Deploy:**
   - The build will install on your Quest automatically
   - Make sure `Server.py` is running before launching the app on Quest

## Configuration

### MediaPipe Settings (Server.py)

- `MODEL_COMPLEXITY`: 0-3 (higher = more accurate but slower, default: 1)
- `TARGET_FPS`: Frame rate limit (default: 30)
- Camera resolution: Set via `cap.set(3, 1280)` and `cap.set(4, 720)`

### Unity Settings (BodyTracking Component)

- `scale`: Scale factor for MediaPipe coordinates
- `xOffset`: X-axis offset for positioning
- `mirrorX`: Mirror X-axis to fix left/right swapping
- `cameraImageWidth`: Should match Server.py camera width
- `smoothingFactor`: Higher = smoother but more lag
- `useVelocityFilter`: Enable velocity-based filtering
- `maxMovementPerFrame`: Maximum allowed movement per frame

### HybridBodyProvider Settings

- `overwriteHips`: Enable/disable hip position overwrite
- `overwriteLegs`: Enable/disable leg rotation overwrite
- `overwriteFeet`: Enable/disable foot/ankle overwrite

## Troubleshooting

### Python Script Issues

**"No module named 'cv2'" or "No module named 'mediapipe'"**
- Run: `pip install opencv-python mediapipe`

**Camera not found**
- Check that your webcam is connected and not being used by another application
- Try changing `cv2.VideoCapture(0)` to `cv2.VideoCapture(1)` or another index

**Connection refused errors**
- Make sure Unity is running and the UDPReceive component is active
- Check that the IP address and port match between Python and Unity
- If testing in Editor, use `127.0.0.1` or `localhost`
- If building for Quest, ensure both devices are on the same Wi-Fi network

### Unity Issues

**No pose data appearing**
- Check that `Server.py` is running
- Verify UDP port matches (default: 5052)
- Check Unity Console for errors
- Ensure `UDPReceive` component is assigned to `BodyTracking`

**Avatar not moving**
- Verify `HybridBodyProvider` is in the scene and assigned correctly
- Check that Character Retargeting is configured on your avatar
- Ensure the source data provider is set to use `HybridBodyProvider`

**Jittery tracking**
- Increase `smoothingFactor` in `BodyTracking` component
- Enable `useVelocityFilter` and adjust `maxMovementPerFrame`
- Enable `useExtraDepthSmoothing` for Z-axis (depth) smoothing

## Project Structure

```
FBT-Debugging-Client/
├── Server.py                          # Python MediaPipe pose detection server
├── Assets/
│   └── FBT_Scripts/
│       ├── BodyTracking.cs            # Receives and processes MediaPipe data
│       ├── HybridBodyProvider.cs      # Combines MediaPipe + Meta tracking
│       ├── Receiver.cs                # UDP receiver for pose data
│       └── LineCode.cs                # Visualizes skeleton with lines
└── README.md                          # This file
```

## How It Works

1. **Python Script (`Server.py`):**
   - Captures webcam feed
   - Processes frames with MediaPipe Pose detection
   - Extracts 33 body landmarks
   - Sends landmark coordinates via UDP to Unity

2. **Unity Receiver (`Receiver.cs`):**
   - Receives UDP data on a background thread
   - Queues data for main thread processing
   - Provides thread-safe data access

3. **Body Tracking (`BodyTracking.cs`):**
   - Parses UDP landmark data
   - Applies smoothing and filtering
   - Updates particle system to visualize landmarks
   - Provides smoothed positions for other components

4. **Hybrid Body Provider (`HybridBodyProvider.cs`):**
   - Extends Meta's `MetaSourceDataProvider`
   - Gets standard Meta body tracking data
   - Patches in MediaPipe data for hips, legs, and feet
   - Returns hybrid skeleton data to Character Retargeting system

5. **Character Retargeting:**
   - Takes the hybrid skeleton data
   - Maps it to your avatar's skeleton
   - Applies the pose to the avatar

## Notes

- The MediaPipe data coordinate system may need adjustment depending on your camera setup
- Leg swapping is handled automatically, but may need tweaking if coordinate systems don't align
- Hip position overwrite is currently disabled by default due to scaling/positioning issues
- All MediaPipe data is in local space relative to the root transform for VR compatibility