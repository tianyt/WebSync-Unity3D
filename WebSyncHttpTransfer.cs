using System.Collections.Specialized;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using FM;
using FM.WebSync;

/// <summary>
/// Http transport layer uses Unity WWW
/// </summary>
public class WebSyncHttpTransfer : HttpTransfer
{
	/// <summary>
	/// Singleton instance
	/// </summary>
	private static WebSyncHttpTransfer m_instance = null;
	
	/// <summary>
	/// Singleton interface
	/// </summary>
	public static WebSyncHttpTransfer Singleton
	{
		get
		{
			if (null == m_instance)
			{
				Debug.Log("Constructing WebSyncHttpTransfer...");
				m_instance = new WebSyncHttpTransfer();
			}
			
			return m_instance;
		}
	}	
	
	/// <summary>
	/// Force only a single instance
	/// </summary>
	private WebSyncHttpTransfer()
	{
	}
	
    public class TransferData
    {
        /// <summary>
        /// Avoid security exceptions in the web player by using Unity WWW
        /// </summary>
        public WWW m_www = null;

        /// <summary>
        /// WWW is done
        /// </summary>
        public bool m_isDone = false;

        /// <summary>
        /// WWW has begun
        /// </summary>
        private bool m_hasBegun = false;

        /// <summary>
        /// Pass websync request args
        /// </summary>
        public HttpRequestArgs m_requestArgs = null;

        /// <summary>
        /// Pass websync response args
        /// </summary>
        public HttpResponseArgs m_responseArgs = null;

        /// <summary>
        /// Callback for handling response
        /// </summary>
        public SingleAction<HttpResponseArgs> m_callback = null;

        /// <summary>
        /// is callback synchronous
        /// </summary>
        public bool m_synchronous = false;

        /// <summary>
        /// Modes
        /// </summary>
        public enum Mode
        {
            Connect,
            Publish,
            Subscribe,
        }

        /// <summary>
        /// Mode just for debugging
        /// </summary>
        public Mode m_mode = Mode.Connect;

        /// <summary>
        /// Content Modes
        /// </summary>
        public enum ContentMode
        {
            Binary,
            Text,
        }

        /// <summary>
        /// Content mode for the request
        /// </summary>
        public ContentMode m_contentMode = ContentMode.Text;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="syncronous"></param>
        public TransferData(bool syncronous)
        {
            m_synchronous = syncronous;
        }

        /// <summary>
        /// Use unity WWW to proxy the http request
        /// </summary>
        public void Begin()
        {
            if (m_hasBegun)
                return;

            m_hasBegun = true;

            Debug.Log("Beginning TransferData...");

            if (m_requestArgs != null)
            {
                switch (m_contentMode)
                {
                    case ContentMode.Binary:
                        m_www = new WWW(m_requestArgs.Url, m_requestArgs.BinaryContent);
                        break;
                    case ContentMode.Text:
                        m_www = new WWW(m_requestArgs.Url, System.Text.Encoding.UTF8.GetBytes(m_requestArgs.TextContent));
                        break;
                }
            }
            else
            {
                Debug.Log("Request Args were null!");
            }
        }

        /// <summary>
        /// Keep proxy pumping
        /// </summary>
        public void Update()
        {
            try
            {
                if (m_isDone)
                    return;

                if (m_www == null)
                {
                    Debug.LogWarning("WWW object was null.");
                    m_isDone = true;
                }
                else if (m_www != null && m_www.error == null && m_www.isDone)
                {
                    if (!string.IsNullOrEmpty(m_www.text))
                    {
                        if (m_www.text.StartsWith(@"[{""channel"":""\/meta\/connect"","))
                            m_mode = Mode.Subscribe;
                        Debug.Log(string.Format("mode={0} text={1}", m_mode, m_www.text));
                    }
                    if (m_www.bytes != null)
                    {
                        string contentKey = string.Empty;
                        string contentType = string.Empty;

                        byte[] copyBytes;

                        if (m_www.bytes.Length != 0)
                        {
                            copyBytes = new byte[m_www.bytes.Length];
                            Array.Copy(m_www.bytes, copyBytes, m_www.bytes.Length);
                        }
                        else
                            copyBytes = new byte[0];

                        m_responseArgs = new HttpResponseArgs(m_requestArgs);
                        m_responseArgs.BinaryContent = copyBytes;
                        m_responseArgs.StatusCode = 200;
                        m_responseArgs.TextContent = System.Text.Encoding.UTF8.GetString(copyBytes);

                        Debug.Log(string.Format("TextContent: {0}", m_responseArgs.TextContent));
                        
                        foreach (KeyValuePair<string, string> kvp in m_www.responseHeaders)
                        {
                            m_responseArgs.Headers.Add(kvp.Key, kvp.Value);
                            //Debug.Log(string.Format("m_www.responseHeaders: {0} | {1}", kvp.Key, kvp.Value));
                        }

                        foreach (string key in m_responseArgs.Headers.AllKeys)
                        {
                            Debug.Log(string.Format("Passed Header: {0} | {1}", key, m_responseArgs.Headers[key]));
                        }

                        Debug.Log("WebSyncHttpTransfer.Update: Prepared response args");

                        // Done getting the bytes, kill the WWW
                        m_www.Dispose();
                        m_www = null;

                        m_isDone = true;

                        Debug.Log("WebSyncHttpTransfer.Update: Transfer data is done");
                    }
                }
                else if (m_www.error != null)
                {
                    if (m_www.error != "WWW request was cancelled")
                    {
                        Debug.LogError(string.Format("Error in WWW object: {0}", m_www.error));
                    }

                    m_www.Dispose();
                    m_www = null;

                    m_isDone = true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format("Update exception={0}", ex));
            }
        }

        /// <summary>
        /// Invoke the response
        /// </summary>
        public void SendResponse()
        {
            try
            {
                if (null != m_callback &&
                    null != m_responseArgs)
                {
                    Debug.Log("TransferData invoking callback");
                    m_callback.Invoke(m_responseArgs);
                    Debug.Log("TransferData invoked callback");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format("Failed to invoke callback exception={0}", ex));
            }
        }
    }

    /// <summary>
    /// Pending proxy data
    /// </summary>
    public List<TransferData> m_transferData = new List<TransferData>();

    /// <summary>
    /// Completed proxy data
    /// </summary>
    public List<TransferData> m_completedAsyncData = new List<TransferData>();

    /// <summary>
    /// Add to proxy data
    /// </summary>
    /// <param name="td"></param>
    private void AddToTransferData(TransferData td)
    {
        m_transferData.Add(td);
    }

    /// <summary>
    /// Get the text content from the request args
    /// </summary>
    /// <param name="requestArgs"></param>
    /// <returns></returns>
    private string GetTextContent(HttpRequestArgs requestArgs)
    {
        if (null == requestArgs)
        {
            return string.Empty;
        }

        if (null == requestArgs.TextContent)
        {
            return string.Empty;
        }

        if (requestArgs.TextContent.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            string data = requestArgs.TextContent;
            if (string.IsNullOrEmpty(data))
            {
                return string.Empty;
            }
            return data;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Get the binary content from the request args
    /// </summary>
    /// <param name="requestArgs"></param>
    /// <returns></returns>
    private string GetBinaryContent(HttpRequestArgs requestArgs)
    {
        if (null == requestArgs)
        {
            return string.Empty;
        }

        if (null == requestArgs.BinaryContent)
        {
            return string.Empty;
        }

        if (requestArgs.BinaryContent.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            string data = System.Text.Encoding.UTF8.GetString(requestArgs.BinaryContent);
            if (string.IsNullOrEmpty(data))
            {
                return string.Empty;
            }
            return data;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Show info for debugging
    /// </summary>
    /// <param name="info"></param>
    /// <param name="requestArgs"></param>
    private void ShowDebugInfo(System.Reflection.MethodBase info, TransferData.ContentMode contentMode, HttpRequestArgs requestArgs)
    {
        try
        {
            Debug.Log(string.Format("Method={0} url={1} content={2} sender={3} OnRequestCreated={4} OnResponseReceived={5}",
                       (null == info) ? "(empty)" : info.ToString(),
                       (null == requestArgs || string.IsNullOrEmpty(requestArgs.Url))
                           ? "(empty)"
                           : requestArgs.Url,
                           contentMode == TransferData.ContentMode.Binary ? GetBinaryContent(requestArgs) : GetTextContent(requestArgs),
                       (null == requestArgs || null == requestArgs.Sender) ? "(empty)" : requestArgs.Sender,
                       (null == requestArgs || null == requestArgs.OnRequestCreated ||
                        null == requestArgs.OnRequestCreated.Target)
                           ? "(empty)"
                           : requestArgs.OnRequestCreated.Target.ToString(),
                       (null == requestArgs || null == requestArgs.OnResponseReceived ||
                        null == requestArgs.OnResponseReceived.Target)
                           ? "(empty)"
                           : requestArgs.OnResponseReceived.Target.ToString()));
        }
        catch (System.Exception)
        {
        }
    }

    /// <summary>
    /// Implements transport callback
    /// </summary>
    /// <param name="requestArgs"></param>
    /// <param name="callback"></param>
    public void SendAsynchronous(TransferData.ContentMode contentMode, HttpRequestArgs requestArgs, SingleAction<HttpResponseArgs> callback)
    {
        ShowDebugInfo(System.Reflection.MethodBase.GetCurrentMethod(), contentMode, requestArgs);

        TransferData td = new TransferData(false)
                              {
                                  m_contentMode = contentMode,
                                  m_synchronous = false,
                                  m_requestArgs = requestArgs,
                                  m_callback = callback
                              };

        AddToTransferData(td);
    }

    /// <summary>
    /// Implements transport callback
    /// </summary>
    /// <param name="requestArgs"></param>
    /// <param name="callback"></param>
    public void SendContentAsynchronous(TransferData.ContentMode contentMode, HttpRequestArgs requestArgs, SingleAction<HttpResponseArgs> callback)
    {
        ShowDebugInfo(System.Reflection.MethodBase.GetCurrentMethod(), contentMode, requestArgs);

        TransferData td = new TransferData(false)
                              {
                                  m_contentMode = contentMode,
                                  m_synchronous = false,
                                  m_requestArgs = requestArgs,
                                  m_callback = callback
                              };

        AddToTransferData(td);
    }

    /// <summary>
    /// Implements transport callback
    /// </summary>
    /// <param name="requestArgs"></param>
    /// <returns></returns>
    public HttpResponseArgs SendSynchronous(TransferData.ContentMode contentMode, HttpRequestArgs requestArgs)
    {
        ShowDebugInfo(System.Reflection.MethodBase.GetCurrentMethod(), contentMode, requestArgs);

        TransferData td = new TransferData(true)
                              {
                                  m_contentMode = contentMode,
                                  m_synchronous = true,
                                  m_requestArgs = requestArgs
                              };

        AddToTransferData(td);

        while (!td.m_isDone)
        {
            System.Threading.Thread.Sleep(0);
        }

        return td.m_responseArgs;
    }

    /// <summary>
    /// Keep proxy coming and going and pumping
    /// </summary>
    public void Update()
    {
        // use index instead of foreach so collection can be modified
        int index = 0;
        while (index < m_transferData.Count)
        {
            TransferData item = m_transferData[index];
            item.Begin();
            item.Update();
            if (item.m_isDone)
            {
                m_transferData.RemoveAt(index);
                Debug.Log(string.Format("WebSyncHttpHandler.Update TransferData is done m_synchronous={0}",
                                        item.m_synchronous));
                if (!item.m_synchronous)
                {
                    Debug.Log("WebSyncHttpHandler.Update Added complete args");
                    m_completedAsyncData.Add(item);
                }
            }
            else
            {
                ++index;
            }
        }

        //Debug.Log("Exit WebSyncHttpTransfer.Update");
    }

    /// <summary>
    /// Implements transport callback
    /// </summary>
    public new void SendAsync(HttpRequestArgs requestArgs, SingleAction<HttpResponseArgs> callback)
    {
        SendContentAsynchronous(TransferData.ContentMode.Binary, requestArgs, callback);
    }

    public override HttpResponseArgs SendBinary(HttpRequestArgs requestArgs)
    {
        return SendSynchronous(TransferData.ContentMode.Binary, requestArgs);
    }

    public override void SendBinaryAsync(HttpRequestArgs requestArgs, SingleAction<HttpResponseArgs> callback)
    {
        SendContentAsynchronous(TransferData.ContentMode.Binary, requestArgs, callback);
    }

    public override HttpResponseArgs SendText(HttpRequestArgs requestArgs)
    {
        return SendSynchronous(TransferData.ContentMode.Text, requestArgs);
    }

    public override void SendTextAsync(HttpRequestArgs requestArgs, SingleAction<HttpResponseArgs> callback)
    {
        SendContentAsynchronous(TransferData.ContentMode.Text, requestArgs, callback);
    }

    /// <summary>
    /// Shutdown the proxy
    /// </summary>
    public override void Shutdown()
    {
        Debug.Log("WebSyncHttpTransfer Shutdown");
    }
}