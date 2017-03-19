using System.Collections;
using System.Collections.Generic;
using Barebones.Logging;
using UnityEngine;

/// <summary>
/// Handles log controls
/// </summary>
public class LogController : MonoBehaviour {

    [Header("General Logging")]
    [Tooltip("Overrides log levels of all loggers that are set to Global")]
    public LogLevel GlobalLogLevel = LogLevel.Warn;

    [Tooltip("Overrides all log levels")]
    public LogLevel ForceLogLevel = LogLevel.Off;

    private static LogController _instance;

    public static LogController Instance
    {
        get
        {
            if (_instance == null)
            {
                // Try to find an existing controller
                _instance = FindObjectOfType<LogController>();

                if (_instance == null)
                {
                    // Create a default log controller
                    var go = new GameObject("LogController");
                    _instance = go.AddComponent<LogController>();
                    _instance.InitializeLogs();
                    
                    Logs.Warn("No LogController found in the scene, so a new one was created manually with default settings");
                }
            }

            return _instance;
        }
    }
    

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // Delete duplicate controllers
            Destroy(this);
            return;
        }

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        InitializeLogs();
    }

    /// <summary>
    /// Initializes logs (sets up appenders and overrides log levels)
    /// </summary>
    public virtual void InitializeLogs()
    {
        if (!LogManager.IsInitialized)
        {
            LogManager.Initialize(new List<LogHandler> { LogAppenders.UnityConsoleAppender },
                GlobalLogLevel);

            LogManager.ForceLogLevel = ForceLogLevel;
        }
    }
}
