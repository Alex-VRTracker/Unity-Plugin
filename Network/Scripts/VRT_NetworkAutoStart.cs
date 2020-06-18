﻿using System.Collections;
using UnityEngine;
using System.Globalization;
using VRTracker.Manager;
using Torrus.Settings;

/**
 * VR Tracker - Network
 **/

namespace VRTracker.Network
{

    /// <summary>
    /// VR Tracker network auto start
    /// Tries to find a Server for 2 seconds using NetworkDiscovery
    /// and starts a Host or Server if the server wasn't found 
    /// </summary>
    public class VRT_NetworkAutoStart : MonoBehaviour
    {

        [Tooltip("Check to enable host mode")]
        public bool host = false; //Disabled by default on android devices

        public VRTracker.Network.VRT_NetworkDiscovery networkDiscovery;
        public VRTracker.Network.VRT_NetworkManager networkManager;
        private bool hostFound = false;
        private int timeOut = 10;
        private int currentConnectionTime = 0;

        [SerializeField]
        private string serverIp = "192.168.1.113";

        // Use this for initialization
        void Start()
        {
            if (GetComponent<UnetServerIP>().GetServerIp(ref serverIp))
            {
                networkManager.JoinGame(serverIp);
            }
            else
            {
                currentConnectionTime = 0;
                networkDiscovery = FindObjectOfType<VRTracker.Network.VRT_NetworkDiscovery>();
                networkManager = FindObjectOfType<VRTracker.Network.VRT_NetworkManager>();

                // Either we use the Network Discovery if enabled...
                if (networkDiscovery != null && networkDiscovery.enabled)
                {
                    if (host && SpectatorManager.Instance.spectator)
                    {
                        Debug.LogError("NETWORK: Cannot be host (VRT_NetworkAutoStart) and Spectator (VRT_Manager)");
                    }
                    else if (SpectatorManager.Instance.spectator)
                    {
                        networkManager.StartLanServer();
                        networkDiscovery.StartBroadcast();
                    }
                    else if (host)
                    {
                        networkManager.StartLanHost();
                        networkDiscovery.StartBroadcast();
                    }
                    else
                    {
                        StartCoroutine(WaitForLanBoradcast());
                    }
                }

                // ...Or we use the Gateway to get Server IP
                else
                {
                    if (SpectatorManager.Instance.spectator && host)
                    {
                        Debug.LogError("NETWORK: Cannot be host (VRT_NetworkAutoStart) and Spectator (VRT_Manager)");
                    }
                    else if (SpectatorManager.Instance.spectator)
                    {
                        networkManager.StartLanServer();
                    }
                    else if (host)
                    {
                        networkManager.StartLanHost();
                    }
                    else
                    {
                        StartCoroutine(WaitForServerIP());
                    }
                }
            }
        }


        /// <summary>
        /// Wait for UDP broadcast from the Server
        /// </summary>
        /// <returns>The for lan boradcast.</returns>
		IEnumerator WaitForLanBoradcast()
        {
            while (!hostFound && currentConnectionTime < timeOut)
            {
                yield return new WaitForSeconds(1);
                currentConnectionTime++;
            }
            if (currentConnectionTime >= timeOut)
            {
                Debug.Log("No server found");
                if (SpectatorManager.Instance.spectator)
                {
                    networkManager.StartLanServer();
                    networkDiscovery.StartBroadcast();
                }
                else
                {
                    networkManager.StartLanHost();
                    networkDiscovery.StartBroadcast();
                }
            }
            yield return null;
        }

        /// <summary>
        /// Waits in loop to receive server IP from the Gateway before starting
        /// as a Client
        /// </summary>
        /// <returns></returns>
        IEnumerator WaitForServerIP()
        {
            //while testing
            while (!SpectatorManager.Instance.serverIp.StartsWith("192.168.", System.StringComparison.CurrentCulture))
            {
                yield return new WaitForSeconds(1);
            }

            //Joining the server
            if (networkManager != null)
            {
                networkManager.JoinGame(SpectatorManager.Instance.serverIp);
            }
            yield return null;
        }

        public void BrodcastReception(string ipAddress)
        {
            //TODO: Check if this would work with two instance on the same PC
            if (hostFound || ipAddress == "localhost")
                return;
            hostFound = true;
            networkManager.JoinGame(ipAddress);
        }
    }
}