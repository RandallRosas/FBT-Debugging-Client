using UnityEngine;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;

public class UDPReceive : MonoBehaviour
{
    Thread receiveThread;
    UdpClient client; 
    public int port = 5052;
    private volatile bool startRecieving = true;
    public bool printToConsole = false;
    public string data;

    // Thread-safe queues for main thread operations.
    private ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
    private ConcurrentQueue<string> dataQueue = new ConcurrentQueue<string>();

    // Start the UDP receive thread.
    public void Start()
    {
        startRecieving = true;
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    // Clean up the UDP receiver when the object is disabled.
    private void OnDisable()
    {
        // Ensure thread stops when object is disabled or game stops.
        startRecieving = false;
        
        if (client != null)
        {
            try
            {
                client.Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error closing UDP client: {e.Message}");
            }
        }
        
        if (receiveThread != null && receiveThread.IsAlive)
        {
            if (!receiveThread.Join(1000))
            {
                Debug.LogWarning("UDP receive thread did not terminate gracefully");
            }
        }
    }

    // Process queued data and logs on the main thread.
    private void Update()
    {
        while (logQueue.TryDequeue(out string logMessage))
        {
            Debug.Log(logMessage);
        }
        
        while (dataQueue.TryDequeue(out string newData))
        {
            data = newData;
            if (printToConsole)
            {
                Debug.Log($"UDP Data: {data}");
            }
        }
    }

    /*
        Receive UDP data on a background thread.
        
        This method runs in a separate thread to avoid blocking the main Unity thread.
        All received data is queued and processed in the Update() method on the main thread.

        Had to make this script a bit more complicated due to crashing issues.
    */
    private void ReceiveData()
    {
        try 
        {
            client = new UdpClient(port);

            // Needed to set socket options for better Quest compatibility.
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            
            logQueue.Enqueue($"UDP Receiver started on port {port}");
            
            while (startRecieving)
            {
                try
                {
                    IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] dataByte = client.Receive(ref anyIP);
                    string receivedData = Encoding.UTF8.GetString(dataByte);

                    // Queue data for main thread processing.
                    dataQueue.Enqueue(receivedData);
                }
                catch (SocketException sockErr)
                {
                    // Handle socket errors (closed socket, etc).
                    if (startRecieving)
                    {
                        if (sockErr.SocketErrorCode != SocketError.TimedOut)
                        {
                            logQueue.Enqueue($"UDP Socket Error: {sockErr.SocketErrorCode} - {sockErr.Message}");
                        }
                    }
                    if (!startRecieving) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception err)
                {
                    // Handle any other errors.
                    if (startRecieving)
                    {
                        logQueue.Enqueue($"UDP Receive Error: {err.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            logQueue.Enqueue($"UDP Socket Initialization Error: {e.Message}");
        }
        finally
        {
            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch { }
            }
            logQueue.Enqueue("UDP Receiver thread stopped");
        }
    }
}
