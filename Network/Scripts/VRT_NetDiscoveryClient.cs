using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;
public class VRT_NetDiscoveryClient : NetworkDiscovery
{
    private float timeout = 5f;
    private Dictionary<string, float> lanAddresses = new Dictionary<string, float>();

    void Start()
    {
        startClient();
        StartCoroutine(CleanupExpiredEntries());
    }

    public void startClient()
    {
        Initialize();
        StartAsClient();
    }

    private IEnumerator CleanupExpiredEntries()
    {
        while (true)
        {
            var keys = lanAddresses.Keys.ToList();
            foreach (var key in keys)
            {
                if (lanAddresses[key] <= Time.time)
                {
                    lanAddresses.Remove(key);
                }
            }
            yield return new WaitForSeconds(timeout);
        }
    }

    public override void OnReceivedBroadcast(string fromAddress, string data)
    {
        base.OnReceivedBroadcast(fromAddress, data);

        if (FindObjectOfType<VRTracker.Network.VRT_NetworkAutoStart>())
            FindObjectOfType<VRTracker.Network.VRT_NetworkAutoStart>().BrodcastReception(fromAddress);

        if (lanAddresses.ContainsKey(fromAddress) == false)
        {
            lanAddresses.Add(fromAddress, Time.time + timeout);
        }
        else
        {
            lanAddresses[fromAddress] = Time.time + timeout;
        }
    }
}
