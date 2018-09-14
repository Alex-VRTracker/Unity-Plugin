﻿using UnityEngine;
using System.Collections;
using System;
using UnityEngine.VR;
using UnityEngine.Networking;
using System.Collections.Generic;



namespace VRTracker.Manager
{
	/// <summary>
	/// VR Tracker
	/// This script handle all the interaction with a tag
	/// You need to add this component to a child object of VR Tracker depending on the number of tracked object you want
	/// </summary>
    public class VRT_Tag : MonoBehaviour
    {
        [Tooltip("Set the type of Tag to access it later. Each type must only be used once.")]
        public TagType tagType; // The tag type is used to get the tag association across multiple scene and player avatar


        public enum TagType
        {
            Head, 				// Track the user head
			Gun, 				// Track the user weapon
			LeftController, 	// Track the user left controller
			RightController, 	// Track the user right controller
			LeftFoot, 			// Track the user left foot
			RightFoot, 			// Track the user right controller
			CameraSpectator, 	// Track the spectator camera, when a tag is put on a real camera to follow its position
			Other 				// Use to track any object with a tag
        }

        // Button value saved here for VRTK
        [System.NonSerialized] public bool triggerPressed = false;
        [System.NonSerialized] public bool triggerUp = false;
        [System.NonSerialized] public bool triggerDown = false;
        [System.NonSerialized] public bool buttonPressed = false;
        [System.NonSerialized] public bool buttonUp = false;
        [System.NonSerialized] public bool buttonDown = false;
        [System.NonSerialized] public bool trackpadTouch = false;
        [System.NonSerialized] public bool trackpadUp = false;
        [System.NonSerialized] public bool trackpadDown = false;
        [System.NonSerialized] public Vector2 trackpadXY = Vector2.zero;


        // Gun
        [System.NonSerialized] public bool trigger = false;
        [System.NonSerialized] public bool grab = false;
        [System.NonSerialized] public bool joystick = false;
        [System.NonSerialized] public bool a = false;
        [System.NonSerialized] public bool b = false;
        [System.NonSerialized] public bool x = false;
        [System.NonSerialized] public bool y = false;

        public Action OnGrab;
        public Action OnRelease;
        public Action OnAPressed;
        public Action OnAReleased;
        public Action OnBPressed;
        public Action OnBReleased;
        public Action OnXPressed;
        public Action OnXReleased;
        public Action OnYPressed;
        public Action OnYReleased;
        public Action OnJoystickPressed;
        public Action OnJoystickReleased;

        // Trackpad
        protected int trackpadMaxLeft = 0; 		// Max left (x) value sent by the trackpad
        protected int trackpadMaxRight = 1000;  // Max right (x) value sent by the trackpad
        protected int trackpadMaxUp = 1000; 	// Max up (x) value sent by the trackpad
        protected int trackpadMaxDown = 0; 		// Max down (x) value sent by the trackpad

        // Tag buttons
        public Action OnTriggerDown;  //Occurs when trigger button is down
        public Action OnTriggerUp;        //Occurs when trigger button is up
        public Action OnRedButtonDown;    //Occurs when red button is down
        public Action OnRedButtonUp;  //Occurs when red button is up

        // For Quaternion orientation from Tag
        protected bool orientationUsesQuaternion = false; 
        protected Quaternion orientation_quat; // Tag V2 and above
        protected Vector3 orientation_; // Tag V1 and old V2 (not udpated)
        protected Vector3 acceleration_;

        protected float currentTime; //Timestamp use for assignation
        private long initialTimeMs = -1; // Time at start in milliseconds

		public string status; 					//Tag status (unassigned, tracked, lost)
        public int battery; 					//Battery remaining for the tag, in percentage
        [System.NonSerialized] public bool waitingForID = false; // if the tag is Waiting for its ID
        [System.NonSerialized] public bool IDisAssigned = false; // if the script is assigned to a tag
        protected Vector3 positionReceived;		//Position received from VR Tracker system

        public string UID = "Enter Your Tag UID";	//Tag UID, corresponding to the unique id of a tag

        protected NetworkIdentity netId;	//Network identity from UNET, used to get local player

        public bool positionFilter = true; // Check to enable position filtering
        private VRTracker.Utils.VRT_PositionFilter filter = new VRTracker.Utils.VRT_PositionFilter();


        // Use this for initialization
        protected virtual void Start()
        {
            // Get the time at start
            if (initialTimeMs < 1)
                initialTimeMs = System.DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond;
            
            //Check if local player in UNET
            netId = transform.GetComponentInParent<NetworkIdentity>();
            if (netId != null && !netId.isLocalPlayer)
                return;

            VRTracker.Manager.VRT_Manager.Instance.AddTag(this);

            // Valdated the Tag is assigned via hardcoded UID
            if (UID != "Enter Your Tag UID")
                IDisAssigned = true;
        }

        protected virtual void LateUpdate()
        {
            //UNET Check
            if (netId != null && !netId.isLocalPlayer)
                return;

            if (positionFilter)
                this.transform.position = filter.GetPosition(((System.DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond)-initialTimeMs) / 1000.0d);
            else
                this.transform.position = this.positionReceived;

            this.transform.rotation = orientation_quat;
            

            // For pairing purposes
			if (waitingForID)
            {
                currentTime -= Time.deltaTime;
                if (currentTime <= 0)
                {
                    //Assignation time off
                    currentTime = 0;
                    waitingForID = false;
                    IDisAssigned = false;
                }
            }

        }

		/// <summary>
		/// Gets the orientation of the tag
		/// </summary>
		/// <returns>The orientation.</returns>
        public Vector3 getOrientation()
        {
            if (orientationUsesQuaternion)
                return orientation_quat.eulerAngles;
            else
                return orientation_;
        }

        /// <summary>
        /// Reset Headset orientation and Tag orientation offset
        /// </summary>
        public void ResetOrientation()
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive)
                UnityEngine.XR.InputTracking.Recenter();
        }


		/// <summary>
		/// Updates the position and add the timestamp
		/// Currently not used
		/// </summary>
		/// <param name="position_">Position.</param>
		/// <param name="timestamp">Timestamp.</param>
        public void UpdatePosition(Vector3 position_, int timestamp)
        {
            UpdatePosition(position_);
        }

        /// <summary>
        /// Updates the position and store the data
        /// </summary>
        /// <param name="position_">Position.</param>
        public void UpdatePosition(Vector3 position_)
        {
            // PREDICTION
            this.positionReceived = position_;
            if (positionFilter)
                filter.AddPositionMeasurement(((System.DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond) - initialTimeMs) / 1000.0d, position_);
        }

        /// <summary>
        /// Updates the Oriention from IMU For Tag V1
        /// </summary>
        /// <param name="neworientation">Neworientation.</param>
        public void UpdateOrientation(Vector3 neworientation)
        {
            orientation_ = neworientation;
            orientationUsesQuaternion = false;
        }

        /// <summary>
        /// Updates the Oriention from IMU For Tag V2
        /// </summary>
        /// <param name="neworientation">Neworientation.</param>
        public void UpdateOrientationQuat(Quaternion neworientation)
        {
            orientationUsesQuaternion = true;
            orientation_quat = neworientation;
            orientation_quat = orientation_quat * Quaternion.Euler(180f, 0, 0);
            orientation_ = orientation_quat.eulerAngles;
        }


		/// <summary>
		/// Updates the orientation and acceleration from tag data
		/// </summary>
		/// <param name="neworientation">Neworientation.</param>
		/// <param name="newacceleration">Newacceleration.</param>
        public void UpdateOrientationAndAcceleration(Vector3 neworientation, Vector3 newacceleration)
        {
            //TODO: Review this
            Vector3 flippedRotation = new Vector3(-neworientation.z, neworientation.x+90.0f, neworientation.y);

            Quaternion quattest = Quaternion.Euler(flippedRotation);
            quattest = quattest * Quaternion.Euler(180f, 0, 0);
            quattest = quattest * Quaternion.Euler(0, -90f, 0);
            orientation_ = quattest.eulerAngles;
            acceleration_ = newacceleration;
            orientationUsesQuaternion = false;
        }

        /// <summary>
        /// Updates the orientation using quaternion and acceleration from tag data
        /// </summary>
        /// <param name="neworientation">Neworientation.</param>
        /// <param name="newacceleration">Newacceleration.</param>
        public void UpdateOrientationAndAcceleration(Quaternion neworientation, Vector3 newacceleration)
        {
            orientationUsesQuaternion = true;
            orientation_quat = neworientation;
           
            // For TAG V3 only
            orientation_quat = new Quaternion(-neworientation.x, neworientation.y, -neworientation.z, neworientation.w);

            orientation_ = orientation_quat.eulerAngles;
            orientation_.y -= VRT_Manager.Instance.roomNorthOffset;
            orientation_quat = Quaternion.Euler(orientation_);

            // Convert acceleration axis
            // TAG V2
            //acceleration_ = new Vector3(newacceleration.x, newacceleration.z, newacceleration.y);
            // TAG V3
             acceleration_ = new Vector3(-newacceleration.x, newacceleration.z, -newacceleration.y);

            // Transform acceleration from local to world coordinate
            acceleration_ = orientation_quat * acceleration_;

          //   Debug.Log("ACC X " + acceleration_.x.ToString("0.00") + "  Y " + acceleration_.y.ToString("0.00") + "  Z " + acceleration_.z.ToString("0.00"));

            if(positionFilter)
                filter.AddAccelerationMeasurement(((System.DateTime.Now.Ticks / System.TimeSpan.TicksPerMillisecond) - initialTimeMs) / 1000.0d, acceleration_);
        }



		/// <summary>
		/// Raises the special command event.
		/// </summary>
		/// <param name="data">Data.</param>
        public void OnSpecialCommand(string data)
        {
            if (data.Contains("triggeron"))
            {
                if(OnTriggerDown != null)
                    OnTriggerDown();
                triggerPressed = true;
                triggerDown = true;
                triggerUp = false;
            }
            else if (data.Contains("triggeroff"))
            {
                if (OnTriggerUp != null)
                    OnTriggerUp();
                triggerPressed = false;
                triggerUp = true;
            }
            else if (data.Contains("buttonon"))
            {
                if (OnRedButtonDown != null)
                    OnRedButtonDown();
                buttonPressed = true;
                buttonDown = true;
                buttonUp = false;
            }
            else if (data.Contains("buttonoff"))
            {
                if (OnRedButtonUp != null)
                    OnRedButtonUp();
                buttonPressed = false;
                buttonUp = true;
            }
        }

		/// <summary>
		/// Raises the special command sent to all tags
		/// </summary>
		/// <param name="tagID">Tag I.</param>
		/// <param name="data">Data.</param>
        public void OnSpecialCommandToAll(string tagID, string data)
        {
            if (waitingForID && data.Contains("buttonon"))
            {
                UID = tagID;
                IDisAssigned = true;
                waitingForID = false;
            }
        }

		/// <summary>
		/// Raises the tag data event.
		/// Handle the data received from the tag
		/// </summary>
		/// <param name="data">Data.</param>
        public void OnTagData(string data)
        {
            //Debug.LogWarning(data);
            string[] sensors = data.Split(new string[] { "&s=" }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < sensors.Length; i++)
            {
                string[] parameters = sensors[i].Split('&');
                char[] sensorInfo = parameters[0].ToCharArray();
                if (sensorInfo.Length != 2)
                    return;
                Dictionary<string, string> values = new Dictionary<string, string>();
                for (int j = 1; j < parameters.Length; j++)
                {
                    string[] dict = parameters[j].Split('=');
                    values.Add(dict[0], dict[1]);
                }

                // IMU
                if (sensorInfo[0] == '1')
                {
                    Vector3 rec_orientation;
                    Vector3 rec_acceleration;

                    double f;
                    double.TryParse(values["ox"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f);
                    rec_orientation.x = (float)f;
                    double.TryParse(values["oy"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f);
                    rec_orientation.y = (float)f;
                    double.TryParse(values["oz"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f);
                    rec_orientation.z = (float)f;

                    double.TryParse(values["ax"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f);
                    rec_acceleration.x = (float)(f * (9.80665 / 1000.0f));
                    double.TryParse(values["ay"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f);
                    rec_acceleration.y = (float)(f * (9.80665 / 1000));
                    double.TryParse(values["az"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out f);
                    rec_acceleration.z = (float)(f * (9.80665 / 1000));

                    UpdateOrientationAndAcceleration(rec_orientation, rec_acceleration);
                }

                // Trackpad
                else if (sensorInfo[0] == '3')
                {
                    string press = values["st"];
                    if (press == "2")
                    {
                        trackpadTouch = false;
                        trackpadUp = true;
                    }
                    else if (press == "1" || press == "3")
                    {
                        trackpadTouch = true;
                        trackpadDown = true;
                    }
                    float a, b;
                    float.TryParse(values["x"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out a);
                    trackpadXY.y = -(a - (Mathf.Abs(trackpadMaxLeft - trackpadMaxRight) / 2)) / Mathf.Abs(trackpadMaxLeft - trackpadMaxRight);
                    float.TryParse(values["y"], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out b);
                    trackpadXY.x = -(b - (Mathf.Abs(trackpadMaxUp - trackpadMaxDown) / 2)) / Mathf.Abs(trackpadMaxUp - trackpadMaxDown);
                    if (a == 0.0f && b == 0.0f)
                        trackpadXY = Vector2.zero;
                }
            }
        }

		/// <summary>
		/// Raises the tag data event.
		/// Handle the binary data received from the tag
		/// </summary>
		/// <param name="data">Data.</param>
        public virtual void OnTagData(byte[] data)
        {
            //Debug.Log("UDP size " + data.Length + "  Timestamp " + System.DateTime.Now.Millisecond);
            bool parsed = false;
            while (!parsed)
            {
                int i = 2;

                switch (data[i] >> 4 & 0x0F)
                {
                    // IMU
                    case 1:
                        {
                            float ox = ((data[i + 4] << 8) + data[i + 5]) / 100;
                            float oy = ((data[i + 6] << 8) + data[i + 7]) / 100;
                            float oz = ((data[i + 8] << 8) + data[i + 9]) / 100;
                            bool isNeg = (data[i + 10] & 0x80) == 0x80 ? true : false;
                            int ax = isNeg ? (((data[i + 10] & 0x7F) << 8) + data[i + 11]) - 32768 : (((data[i + 10] & 0x7F) << 8) + data[i + 11]);
                            isNeg = (data[i + 12] & 0x80) == 0x80 ? true : false;
                            int ay = isNeg ? (((data[i + 12] & 0x7F) << 8) + data[i + 13]) - 32768 : (((data[i + 12] & 0x7F) << 8) + data[i + 13]);
                            isNeg = (data[i + 14] & 0x80) == 0x80 ? true : false;
                            int az = isNeg ? (((data[i + 14] & 0x7F) << 8) + data[i + 15]) - 32768 : (((data[i + 14] & 0x7F) << 8) + data[i + 15]);
                            Vector3 rec_orientation = new Vector3(ox, oy, oz);
                            Vector3 rec_acceleration = new Vector3((float)ax* (9.80665f / 1000f), (float)ay* (9.80665f / 1000f), (float)az* (9.80665f / 1000.0f));
                            i += 16;
                            if (i >= data.Length)
                                parsed = true;
                            UpdateOrientationAndAcceleration(rec_orientation, rec_acceleration);
                            break;
                        }
                    // IMU Quaternion
                    case 2:
                        {
                            float accuracy = (data[i + 1] << 8) / 10;
                            float ow = ((float)((data[i + 2] << 8) + data[i + 3]) / 10000) - 1;
                            float ox = -(((float)((data[i + 4] << 8) + data[i + 5]) / 10000) - 1);
                            float oz = -(((float)((data[i + 6] << 8) + data[i + 7]) / 10000) - 1);
                            float oy = -(((float)((data[i + 8] << 8) + data[i + 9]) / 10000) - 1);

                            bool isNeg = (data[i + 10] & 0x80) == 0x80 ? true : false;
                            int ax = isNeg ? (((data[i + 10] & 0x7F) << 8) + data[i + 11]) - 32768 : (((data[i + 10] & 0x7F) << 8) + data[i + 11]);
                            isNeg = (data[i + 12] & 0x80) == 0x80 ? true : false;
                            int ay = isNeg ? (((data[i + 12] & 0x7F) << 8) + data[i + 13]) - 32768 : (((data[i + 12] & 0x7F) << 8) + data[i + 13]);
                            isNeg = (data[i + 14] & 0x80) == 0x80 ? true : false;
                            int az = isNeg ? (((data[i + 14] & 0x7F) << 8) + data[i + 15]) - 32768 : (((data[i + 14] & 0x7F) << 8) + data[i + 15]);
                            Quaternion rec_orientation = new Quaternion(ox, oy, oz, ow);
                            Vector3 rec_acceleration = new Vector3((float)ax * (9.80665f / 1000f), (float)ay * (9.80665f / 1000f), (float)az * (9.80665f / 1000.0f));
                            i += 16;

                            if (i >= data.Length)
                                parsed = true;
                            UpdateOrientationAndAcceleration(rec_orientation, rec_acceleration);
                            break;
                        }
                    // Trackpad
                    case 3:
                        {

                            byte x = data[i + 1];
                            byte y = data[i + 2];
                            //byte pressure = (byte)(data[i + 3] >> 4);
                            byte btn = (byte)(data[i + 3] & 0x0F);

                            if (btn == 2)
                            {
                                trackpadTouch = false;
                                trackpadUp = true;
                            }
                            else if (btn == 1 || btn == 3)
                            {
                                trackpadTouch = true;
                                trackpadDown = true;
                            }

                            trackpadXY.y = (float)-(x - 127.5) / 255;
                            trackpadXY.x = (float)-(y - 127.5) / 255;
                            if (data[i + 1] == 0 && data[i + 2] == 0)
                                trackpadXY = Vector2.zero;
                            i += 4;
                            if (i >= data.Length)
                                parsed = true;
                            break;
                        }

                    // Gun
                    case 6:
                        {
                            // byte buttons : 0 [trigger, grab, joy_d, a, b, x, y] 7
                            byte buttons = data[i + 1];
                            bool new_trigger = (buttons & (1 << 0)) != 0;
                            bool new_grab = (buttons & (1 << 1)) != 0;
                            bool new_joystick = (buttons & (1 << 2)) != 0;
                            bool new_a = (buttons & (1 << 3)) != 0;
                            bool new_b = (buttons & (1 << 4)) != 0;
                            bool new_x = (buttons & (1 << 5)) != 0;
                            bool new_y = (buttons & (1 << 6)) != 0;

                            if (new_trigger != trigger)
                            {
                                trigger = new_trigger;
                                Debug.Log("tag guin trigger");

                                if (trigger)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnTriggerDown);
                                else
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnTriggerUp);
                            }

                            if (new_grab != grab)
                            {
                                grab = new_grab;
                                if (grab && OnGrab != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnGrab);
                                else if (OnRelease != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnRelease);
                            }

                            if (new_joystick != joystick)
                            {
                                joystick = new_joystick;
                                if (joystick && OnJoystickPressed != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnJoystickPressed);
                                else if (OnJoystickReleased != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnJoystickReleased);
                            }

                            if (new_a != a)
                            {
                                a = new_a;
                                if (a && OnAPressed != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnAPressed);
                                else if (OnAReleased != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnAReleased);
                            }

                            if (new_b != b)
                            {
                                b = new_b;
                                if (b && OnBPressed != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnBPressed);
                                else if (OnBReleased != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnBReleased);
                            }

                            if (new_x != x)
                            {
                                x = new_x;
                                if (x && OnXPressed != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnXPressed);
                                else if (OnXReleased != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnXReleased);
                            }

                            if (new_y != y)
                            {
                                y = new_y;
                                if (y && OnYPressed != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnYPressed);
                                else if (OnYReleased != null)
                                    UnityMainThreadDispatcher.Instance().Enqueue(OnYReleased);
                            }

                            i += 4;
                            if (i >= data.Length)
                                parsed = true;
                            break;
                        }

                    default:
                        {
                            Debug.Log("Data could not be parsed : " + data[i].ToString() + "  index: " + i);
                            break;
                        }
                }
            }
        }

		/// <summary>
		/// Gets the position received from the system
		/// </summary>
		/// <returns>The position.</returns>
        public Vector3 GetPosition()
        {
            return this.positionReceived;
        }

		/// <summary>
		/// Waits for assignation in pairing phase
		/// </summary>
		/// <returns>The for assignation.</returns>
		/// <param name="delayToPressButton">Delay to press button.</param>
        public IEnumerator WaitForAssignation(float delayToPressButton)
        {
            //Prepare for assignation
            currentTime = delayToPressButton;
            waitingForID = true;
            while (!IDisAssigned && waitingForID)
            {
                yield return new WaitForSeconds(1);
            }
        }

       
		/// <summary>
		/// Raises the destroy event and remove the tag from the list in the manager
		/// </summary>
        protected void OnDestroy()
        {
            if (VRTracker.Manager.VRT_Manager.Instance)
                VRTracker.Manager.VRT_Manager.Instance.RemoveTag(this);
        }

		/// <summary>
		/// Assigns the tag and ask for assignment in the gateway
		/// Used for automatic pairing
		/// </summary>
		/// <param name="tagID">Tag unique id</param>
        public void AssignTag(string tagID)
        {
            UID = tagID;
            IDisAssigned = true;
            waitingForID = false;
            VRTracker.Manager.VRT_Manager.Instance.vrtrackerWebsocket.AssignTag(tagID);
        }
    }
}