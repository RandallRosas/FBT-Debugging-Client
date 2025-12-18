using UnityEngine;
using Unity.Collections;
using Meta.XR.Movement;
using static Meta.XR.Movement.MSDKUtility;

// Need to wrap the entire class in this namespace to inherit from MetaSourceDataProvider.
namespace Meta.XR.Movement.Retargeting
{
    public class HybridBodyProvider : MetaSourceDataProvider
    {
        [Header("MediaPipe Source")]
        public global::BodyTracking mediaPipeTracker;

        [Header("Hybrid Configuration")]
        public bool overwriteHips = true;
        public bool overwriteLegs = true;
        public bool overwriteFeet = true;

        // MediaPipe indices for the lower body.
        private const int MP_LEFT_HIP = 23;
        private const int MP_RIGHT_HIP = 24;
        private const int MP_LEFT_KNEE = 25;
        private const int MP_RIGHT_KNEE = 26;
        private const int MP_LEFT_ANKLE = 27;
        private const int MP_RIGHT_ANKLE = 28;
        private const int MP_LEFT_HEEL = 29;
        private const int MP_RIGHT_HEEL = 30;
        private const int MP_LEFT_FOOT_INDEX = 31;
        private const int MP_RIGHT_FOOT_INDEX = 32;

        // Meta OVRBody joint indices.
        /* 
            WARNING: Hip overwrite is disabled in the project for now.
            The MediaPipe data needs to be scaled and positioned exactly 
            to match the Meta OVRBody before enabling it. Otherwise it breaks the avatar.
        */
        private const int META_HIPS = 1;
        private const int META_LEFT_LEG_UPPER = 70;
        private const int META_LEFT_LEG_LOWER = 71;
        private const int META_RIGHT_LEG_UPPER = 77;
        private const int META_RIGHT_LEG_LOWER = 78;
        private const int META_LEFT_FOOT_ANKLE = 73;
        private const int META_RIGHT_FOOT_ANKLE = 80;

        // Override the base class method to patch the skeleton data.
        public override NativeArray<NativeTransform> GetSkeletonPose()
        {
            var bodyData = base.GetSkeletonPose();

            if (mediaPipeTracker == null || !IsMediaPipeReady())
            {
                return bodyData;
            }

            if (bodyData.IsCreated)
            {
                PatchSkeletonData(bodyData);
            }

            return bodyData;
        }

        // Check if the MediaPipe data is ready.
        private bool IsMediaPipeReady()
        {
            var positions = mediaPipeTracker.GetSmoothedPositions();
            return positions != null && positions.Length > 0 && positions[MP_LEFT_HIP] != Vector3.zero;
        }

        /*
            Patch the skeleton data to the Meta OVRBody.

            I barely got this working, all of this logic will need to be fixed.
            Since the MediaPipe data is not being scaled and positioned exactly to match the Meta OVRBody,
            I wasn't able to tweak it before turning this in.
        */
        private void PatchSkeletonData(NativeArray<NativeTransform> metaSkeleton)
        {
            Vector3[] mpLocalPositions = mediaPipeTracker.GetSmoothedPositions();
            Transform mpRoot = mediaPipeTracker.GetRootTransform();
            Vector3 ToWorld(int index) => mpRoot.TransformPoint(mpLocalPositions[index]);

            // Overwrite the hips position.
            if (overwriteHips)
            {
                Vector3 leftHip = ToWorld(MP_LEFT_HIP);
                Vector3 rightHip = ToWorld(MP_RIGHT_HIP);
                // Pretty sure this grabs the center of the hips.
                Vector3 centerHip = (leftHip + rightHip) * 0.5f;
                UpdateJointPosition(metaSkeleton, META_HIPS, centerHip);
            }

            // Overwrite the legs rotations.
            if (overwriteLegs)
            {
                Vector3 playerForward = mpRoot.forward;

                // Left leg data.
                Vector3 lHip = ToWorld(MP_LEFT_HIP);  
                Vector3 lKnee = ToWorld(MP_LEFT_KNEE);  
                Vector3 lAnkle = ToWorld(MP_LEFT_ANKLE);

                // The quaternion rotations might need to be tweaked due to the MediaPipe -> Unity rotation conversion.
                Vector3 lThighDir = (lKnee - lHip).normalized;
                Vector3 lShinDir = (lAnkle - lKnee).normalized;
                Quaternion lThighRot = Quaternion.LookRotation(lThighDir, playerForward);
                Quaternion lShinRot = Quaternion.LookRotation(lShinDir, playerForward);
                UpdateJointRotation(metaSkeleton, META_LEFT_LEG_UPPER, lThighRot);
                UpdateJointRotation(metaSkeleton, META_LEFT_LEG_LOWER, lShinRot);

                // Right leg data.
                Vector3 rHip = ToWorld(MP_RIGHT_HIP);  
                Vector3 rKnee = ToWorld(MP_RIGHT_KNEE);  
                Vector3 rAnkle = ToWorld(MP_RIGHT_ANKLE);

                // The quaternion rotations might need to be tweaked due to the MediaPipe -> Unity rotation conversion.
                Vector3 rThighDir = (rKnee - rHip).normalized;
                Vector3 rShinDir = (rAnkle - rKnee).normalized;
                Quaternion rThighRot = Quaternion.LookRotation(rThighDir, playerForward);
                Quaternion rShinRot = Quaternion.LookRotation(rShinDir, playerForward);
                UpdateJointRotation(metaSkeleton, META_RIGHT_LEG_UPPER, rThighRot);
                UpdateJointRotation(metaSkeleton, META_RIGHT_LEG_LOWER, rShinRot);
            }

            // Overwrite the feet/ankles position and rotation.
            if (overwriteFeet)
            {
                /*
                    The left foot data is using RIGHT MediaPipe data at the moment.
                    For some reason the feet are swapped on the Meta OVRBody.
                    It might be a scaling/positioning issue with the MediaPipe data as well.
                */
                Vector3 lAnkle = ToWorld(MP_RIGHT_ANKLE);  
                Vector3 lFootIndex = ToWorld(MP_RIGHT_FOOT_INDEX);  
                Vector3 lHeel = ToWorld(MP_RIGHT_HEEL);  

                // Calculate foot direction from heel to toe. This gives us the foot orientation.
                Vector3 lFootDir = (lFootIndex - lHeel).normalized;
                Vector3 lFootUp = Vector3.Cross(lFootDir, (lAnkle - lHeel).normalized).normalized;

                // If the foot direction and up vector are not zero, update the foot rotation.
                if (lFootDir != Vector3.zero && lFootUp != Vector3.zero)
                {
                    Quaternion lFootRot = Quaternion.LookRotation(lFootDir, lFootUp);
                    UpdateJointRotation(metaSkeleton, META_LEFT_FOOT_ANKLE, lFootRot);
                }

                // Update the ankle position.
                UpdateJointPosition(metaSkeleton, META_LEFT_FOOT_ANKLE, lAnkle);

                /*
                    The right foot. Using the LEFT MediaPipe data at the moment.
                    For some reason the feet are swapped on the Meta OVRBody.
                    It might be a scaling/positioning issue with the MediaPipe data as well.
                */
                Vector3 rAnkle = ToWorld(MP_LEFT_ANKLE);  
                Vector3 rFootIndex = ToWorld(MP_LEFT_FOOT_INDEX);  
                Vector3 rHeel = ToWorld(MP_LEFT_HEEL);  

                // Calculate foot direction from heel to toe. This gives us the foot orientation.
                Vector3 rFootDir = (rFootIndex - rHeel).normalized;
                Vector3 rFootUp = Vector3.Cross(rFootDir, (rAnkle - rHeel).normalized).normalized;

                // If the foot direction and up vector are not zero, update the foot rotation.
                if (rFootDir != Vector3.zero && rFootUp != Vector3.zero)
                {
                    Quaternion rFootRot = Quaternion.LookRotation(rFootDir, rFootUp);
                    UpdateJointRotation(metaSkeleton, META_RIGHT_FOOT_ANKLE, rFootRot);
                }

                // Update the ankle position.
                UpdateJointPosition(metaSkeleton, META_RIGHT_FOOT_ANKLE, rAnkle);
            }
        }

        private void UpdateJointPosition(NativeArray<NativeTransform> data, int index, Vector3 newPos)
        {
            if (index >= 0 && index < data.Length)
            {
                NativeTransform joint = data[index];
                joint.Position = newPos;
                data[index] = joint;
            }
        }

        private void UpdateJointRotation(NativeArray<NativeTransform> data, int index, Quaternion newRot)
        {
            if (index >= 0 && index < data.Length)
            {
                NativeTransform joint = data[index];
                joint.Orientation = newRot;
                data[index] = joint;
            }
        }
    }
}