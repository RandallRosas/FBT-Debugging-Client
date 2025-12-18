import cv2
import mediapipe as mp
import socket
import time

# There are 3 model complexities: 0,1,2,3.
# Left it on the default setting.
MODEL_COMPLEXITY = 1 

# Use localhost when testing in Unity Editor.
# Use the Quest's IP address when building for Quest.
UDP_IP = "192.168.0.165"
UDP_PORT = 5052
TARGET_FPS = 30

# Initialize MediaPipe Pose.
mp_pose = mp.solutions.pose
pose = mp_pose.Pose(
    model_complexity=MODEL_COMPLEXITY,
    smooth_landmarks=True,
    enable_segmentation=False,
    smooth_segmentation=False,
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5,
    static_image_mode=False
)
mp_drawing = mp.solutions.drawing_utils

# Initialize the camera.
cap = cv2.VideoCapture(0)
cap.set(3, 1280)
cap.set(4, 720)

# Initialize the socket.
sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
serverAddressPort = (UDP_IP, UDP_PORT)

# Calculate the frame time.
frame_time = 1.0 / TARGET_FPS
last_frame_time = time.time()

while True:
    # Get image frame
    success, img = cap.read()
    if not success:
        print("Ignoring empty camera frame.")
        continue

    h, w, _ = img.shape
    
    # Process the image for pose.
    img_rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    results = pose.process(img_rgb)
    
    data = []
    
    # Extract and send the MediaPipe body landmarks data.
    if results.pose_landmarks:
        mp_drawing.draw_landmarks(
            img, 
            results.pose_landmarks, 
            mp_pose.POSE_CONNECTIONS
        )
        
        # Extract the MediaPipe body landmarks.
        for lm in results.pose_landmarks.landmark:
            # Convert to pixel space
            posX = lm.x * w
            posY = h - (lm.y * h)
            posZ = lm.z * w
            
            data.extend([posX, posY, posZ])
            
        # Send the MediaPipe body landmarks data via UDP.
        try:
            sock.sendto(str.encode(str(data)), serverAddressPort)
        except Exception as e:
            print(f"Socket Error: {e}")

    # Frame rate limiting to reduce jitter from inconsistent frame times.
    current_time = time.time()
    elapsed = current_time - last_frame_time
    if elapsed < frame_time:
        time.sleep(frame_time - elapsed)
    last_frame_time = time.time()
    
    # Display the image.
    cv2.imshow("Image", img)
    # Exit on the 'q' key.
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()
