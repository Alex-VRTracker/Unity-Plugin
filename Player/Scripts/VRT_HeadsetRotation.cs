﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// VRT headset rotation.
/// This script is to be set on a Gameobject between the Camera and the Object to which the Headset Tag position is applied
/// </summary>
namespace VRTracker.Player {
    public class VRT_HeadsetRotation : MonoBehaviour
    {

        public new Camera camera;
        public new VRTracker.Manager.VRT_Tag tag;

        private NetworkIdentity networkIdentity;

        private Quaternion previousOffset;
        private Quaternion destinationOffset;

        private Vector3 newRotation;

        private float t;
        private float timeToReachTarget = 5.0f;			//Time used to correct the orientation
		private int waitTimeBeforeVerification = 300; 	//Time in second before checking if the orientation need to be corrected
        [Tooltip("The minimum offset in degrees to blink instead of rotating.")]
		public float minOffsetToBlink = 15.0f;			//Minimun difference for the orientation to display a blink and do an hard correction

        [SerializeField] Renderer fader;

        /*[Tooltip("The VRTK Headset Fade script to use when fading the headset. If this is left blank then the script will need to be applied to the same GameObject.")]
        public VRTK.VRTK_HeadsetFade headsetFade;
        */
        void Start()
        {
            if (networkIdentity == null)
                networkIdentity = GetComponentInParent<NetworkIdentity>();
            newRotation = Vector3.zero;
            if (tag == null)
                tag = VRTracker.Manager.VRT_Manager.Instance.GetHeadsetTag();
            if (networkIdentity != null && !networkIdentity.isLocalPlayer)
            {
				gameObject.SetActive (false);
                this.enabled = false;
                return;
            }
            previousOffset = Quaternion.Euler(Vector3.zero);
            destinationOffset = Quaternion.Euler(Vector3.zero);
            ResetOrientation();
            StartCoroutine(FixOffset());
        }

        // Update is called once per frame
        void LateUpdate()
        {
            t += Time.deltaTime / timeToReachTarget;
            transform.localRotation = Quaternion.Lerp(previousOffset, destinationOffset, t);
        }

        IEnumerator Blink()
        {
            Color faderColor = fader.material.color;

            faderColor.a = 1;
            fader.material.color = faderColor;

            yield return new WaitForSeconds(0.1f);
            while (faderColor.a > 0)
            {
                faderColor.a -= 0.2f;
                fader.material.color = faderColor;
                yield return new WaitForSeconds(0.05f);
            }
            faderColor.a = 0;
            fader.material.color = faderColor;
        }

		/// <summary>
		/// Fixes the offset and make the correction if necessary
		/// </summary>
		/// <returns>The offset.</returns>
        IEnumerator FixOffset()
        {
            while (true)
            {
                if (VRTracker.Manager.VRT_Manager.Instance != null)
                {
					yield return new WaitForSeconds(waitTimeBeforeVerification);
                    if (tag == null)
                        tag = VRTracker.Manager.VRT_Manager.Instance.GetHeadsetTag();
                    if (tag != null)
                    {

                        bool isReorienting = NeedReorientation();
                        if (isReorienting)
                        {
                            t = timeToReachTarget;
                            StartCoroutine(Blink());
                        }
                        else
                            t = 0;
                    }
                }
                else
                {
                    yield return new WaitForSeconds(0.5f);
                }
            }
        }

		/// <summary>
		/// Updates the orientation data
		/// </summary>
        private void UpdateOrientationData()
        {
            Vector3 tagRotation = UnmultiplyQuaternion(Quaternion.Euler(tag.getOrientation()));
            Vector3 cameraRotation = UnmultiplyQuaternion(camera.transform.localRotation);
            newRotation.y = tagRotation.y - cameraRotation.y;
            previousOffset = destinationOffset;
            destinationOffset = Quaternion.Euler(newRotation);
        }

		/// <summary>
		/// Returns true if the orientation need to be corrected
		/// </summary>
		/// <returns><c>true</c>, if reorientation was needed, <c>false</c> otherwise.</returns>
        private bool NeedReorientation()
        {
            UpdateOrientationData();
            float offsetY = Mathf.Abs(previousOffset.eulerAngles.y - newRotation.y) % 360;
            offsetY = offsetY > 180.0f ? offsetY - 360 : offsetY;
            return Mathf.Abs(offsetY) > minOffsetToBlink;
        }

		/// <summary>
		/// Resets the orientation and fade a blink to the user
		/// </summary>
        public void ResetOrientation()
        {
		    UpdateOrientationData();
            transform.localRotation = destinationOffset;
            previousOffset = destinationOffset;
            StartCoroutine(Blink());
        }

		/// <summary>
		/// Unmultiplies the quaternion to get the rotation
		/// </summary>
		/// <returns>The quaternion.</returns>
		/// <param name="quaternion">Quaternion.</param>
        private Vector3 UnmultiplyQuaternion(Quaternion quaternion)
        {
            Vector3 ret;

            var xx = quaternion.x * quaternion.x;
            var xy = quaternion.x * quaternion.y;
            var xz = quaternion.x * quaternion.z;
            var xw = quaternion.x * quaternion.w;

            var yy = quaternion.y * quaternion.y;
            var yz = quaternion.y * quaternion.z;
            var yw = quaternion.y * quaternion.w;

            var zz = quaternion.z * quaternion.z;
            var zw = quaternion.z * quaternion.w;

            var check = zw + xy;
            if (Mathf.Abs(check - 0.5f) <= 0.00001f)
                check = 0.5f;
            else if (Mathf.Abs(check + 0.5f) <= 0.00001f)
                check = -0.5f;

            ret.y = Mathf.Atan2(2 * (yw - xz), 1 - 2 * (yy + zz));
            ret.z = Mathf.Asin(2 * check);
            ret.x = Mathf.Atan2(2 * (xw - yz), 1 - 2 * (zz + xx));

            if (check == 0.5f)
            {
                ret.x = 0;
                ret.y = 2 * Mathf.Atan2(quaternion.y, quaternion.w);
            }
            else if (check == -0.5f)
            {
                ret.x = 0;
                ret.y = -2 * Mathf.Atan2(quaternion.y, quaternion.w);
            }

            ret = ret * 180 / Mathf.PI;
            return ret;
        }
    }
}