﻿using UnityEngine;
using UnityEngine.VR;

namespace VRTracker.Pairing
{
	/// <summary>
	/// VRT camera mouse control
	/// Enables the control of the reticule with the mouse
	/// </summary>
    public class VRT_CameraMouseControl : MonoBehaviour
    {

        private Camera mycam;

        // Use this for initialization
        void Start()
        {
            mycam = GetComponent<Camera>();
        }

        // Update is called once per frame
        void Update()
        {
            if (!UnityEngine.XR.XRSettings.isDeviceActive)
            {
                float sensitivity = 0.05f;
                Vector3 vp = mycam.ScreenToViewportPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, mycam.nearClipPlane));
                vp.x -= 0.5f;
                vp.y -= 0.5f;
                vp.x *= sensitivity;
                vp.y *= sensitivity;
                vp.x += 0.5f;
                vp.y += 0.5f;
                Vector3 sp = mycam.ViewportToScreenPoint(vp);

                Vector3 v = mycam.ScreenToWorldPoint(sp);
                transform.LookAt(v, Vector3.up);
            }
        }
    }
}