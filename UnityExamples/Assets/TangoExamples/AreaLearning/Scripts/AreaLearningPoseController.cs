/*
 * Copyright 2014 Google Inc. All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections;
using UnityEngine;
using Tango;
using System;

/// <summary>
/// This is a basic movement controller based on
/// pose estimation returned from the Tango Service.
/// </summary>
public class AreaLearningPoseController : MonoBehaviour, ITangoPose
{   
    [HideInInspector]
    public const int DEVICE_TO_START = 0;
    [HideInInspector]
    public const int DEVICE_TO_ADF = 1;
    [HideInInspector]
    public const int START_TO_ADF = 2;
    
    public float m_movementScale = 1.0f;
    public bool m_useADF = false;
    [HideInInspector]
    public string m_tangoServiceVersionName = string.Empty;

    // Tango pose data for debug logging and transform update.
    // Index 0: device with respect to start frame.
    // Index 1: device with respect to adf frame.
    // Index 2: start with respect to adf frame.
    [HideInInspector]
    public float[] m_frameDeltaTime;
    private float[] m_prevFrameTimestamp;
    [HideInInspector]
    public int[] m_frameCount;
    [HideInInspector]
    public TangoEnums.TangoPoseStatusType[] m_status;
    [HideInInspector]
    public Quaternion[] m_tangoRotation;
    [HideInInspector]
    public Vector3[] m_tangoPosition;
    [HideInInspector]
    public bool m_isRelocalized = false;
    
    private TangoApplication m_tangoApplication;
    private Vector3 m_startingOffset;
    private Quaternion m_startingRotation;

    // We use couple of matrix transformation to convert the pose from Tango coordinate
    // frame to Unity coordinate frame.
    // The full equation is:
    //     Matrix4x4 matrixuwTuc = m_matrixuwTss * matrixssTd * m_matrixdTuc;
    //
    // matrixuwTuc: Unity camera with respect to Unity world, this is the desired matrix.
    // m_matrixuwTss: Constant matrix converting start of service frame to Unity world frame.
    // matrixssTd: Device frame with repect to start of service frame, this matrix denotes the 
    //       pose transform we get from pose callback.
    // m_matrixdTuc: Constant matrix converting Unity world frame frame to device frame.
    //
    // Please see the coordinate system section online for more information:
    //     https://developers.google.com/project-tango/overview/coordinate-systems
    private Matrix4x4 m_matrixuwTss;
    private Matrix4x4 m_matrixdTuc;

    /// <summary>
    /// Determines whether motion tracking is localized.
    /// </summary>
    /// <returns><c>true</c> if motion tracking is localized; otherwise, <c>false</c>.</returns>
    public bool IsLocalized()
    {
        return m_isRelocalized;
    }
    
    /// <summary>
    /// Initialize the controller.
    /// </summary>
    private void Awake()
    {
        m_startingOffset = transform.position;
        m_startingRotation = transform.rotation;
        m_frameDeltaTime = new float[]{-1.0f,-1.0f,-1.0f};
        m_prevFrameTimestamp = new float[]{-1.0f,-1.0f,-1.0f};
        m_frameCount = new int[]{-1,-1,-1};
        m_status = new TangoEnums.TangoPoseStatusType[]{TangoEnums.TangoPoseStatusType.NA,
            TangoEnums.TangoPoseStatusType.NA, TangoEnums.TangoPoseStatusType.NA};
        m_tangoRotation = new Quaternion[]{Quaternion.identity,
            Quaternion.identity, Quaternion.identity};
        m_tangoPosition = new Vector3[]{Vector3.one,Vector3.one,Vector3.one};

        // Constant matrix converting start of service frame to Unity world frame.
        m_matrixuwTss = new Matrix4x4();
        m_matrixuwTss.SetColumn(0, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
        m_matrixuwTss.SetColumn(1, new Vector4(0.0f, 0.0f, 1.0f, 0.0f));
        m_matrixuwTss.SetColumn(2, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
        m_matrixuwTss.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        
        // Constant matrix converting Unity world frame frame to device frame.
        m_matrixdTuc = new Matrix4x4();
        m_matrixdTuc.SetColumn(0, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
        m_matrixdTuc.SetColumn(1, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
        m_matrixdTuc.SetColumn(2, new Vector4(0.0f, 0.0f, -1.0f, 0.0f));
        m_matrixdTuc.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
    }

    /// <summary>
    /// Start this instance.
    /// </summary>
    private void Start()
    {
        m_tangoApplication = FindObjectOfType<TangoApplication>();
        
        if(m_tangoApplication != null)
        {
            if(AndroidHelper.IsTangoCorePresent())
            {
                // Request Tango permissions
                m_tangoApplication.RegisterPermissionsCallback(_OnTangoApplicationPermissionsEvent);
                m_tangoApplication.RequestNecessaryPermissionsAndConnect();
                m_tangoApplication.Register(this);
                m_tangoServiceVersionName = TangoApplication.GetTangoServiceVersion();
            }
            else
            {
                // If no Tango Core is present let's tell the user to install it!
                StartCoroutine(_InformUserNoTangoCore());
            }
        }
        else
        {
            Debug.Log("No Tango Manager found in scene.");
        }
    }
    
    /// <summary>
    /// Informs the user that they should install Tango Core via Android toast.
    /// </summary>
    private IEnumerator _InformUserNoTangoCore()
    {
        AndroidHelper.ShowAndroidToastMessage("Please install Tango Core", false);
        yield return new WaitForSeconds(2.0f);
        Application.Quit();
    }
    
    /// <summary>
    /// Apply any needed changes to the pose.
    /// </summary>
    private void Update()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if(m_tangoApplication != null)
            {
                m_tangoApplication.Shutdown();
            }
            
            // This is a temporary fix for a lifecycle issue where calling
            // Application.Quit() here, and restarting the application immediately,
            // results in a hard crash.
            AndroidHelper.AndroidQuit();
        }
        
        #else
        Vector3 tempPosition = transform.position;
        Quaternion tempRotation = transform.rotation;
        PoseProvider.GetMouseEmulation(ref tempPosition, ref tempRotation);
        transform.rotation = tempRotation;
        transform.position = tempPosition;
        #endif
    }
    
    /// <summary>
    /// Handle the callback sent by the Tango Service
    /// when a new pose is sampled.
    /// DO NOT USE THE UNITY API FROM INSIDE THIS FUNCTION!
    /// </summary>
    /// <param name="callbackContext">Callback context.</param>
    /// <param name="pose">Pose.</param>
    public void OnTangoPoseAvailable(TangoPoseData pose)
    {
        int currentIndex = 0;
        
        // Get out of here if the pose is null
        if (pose == null)
        {
            Debug.Log("TangoPoseDate is null.");
            return;
        }
        
        // The callback pose is for device with respect to start of service pose.
        if (pose.framePair.baseFrame == TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_START_OF_SERVICE &&
            pose.framePair.targetFrame == TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_DEVICE)
        {
            currentIndex = DEVICE_TO_START;
        }
        // The callback pose is for device with respect to area description file pose.
        else if (pose.framePair.baseFrame == TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_AREA_DESCRIPTION &&
                 pose.framePair.targetFrame == TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_DEVICE)
        {
            currentIndex = DEVICE_TO_ADF;
        } 
        // The callback pose is for start of service with respect to area description file pose.
        else if (pose.framePair.baseFrame == TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_AREA_DESCRIPTION &&
                 pose.framePair.targetFrame == TangoEnums.TangoCoordinateFrameType.TANGO_COORDINATE_FRAME_START_OF_SERVICE)
        {
            currentIndex = START_TO_ADF;
        }
        
        // check to see if we are recently relocalized
        if(!m_isRelocalized)
        {
            m_isRelocalized = (currentIndex == 2);
        }
        
        if(pose.status_code == TangoEnums.TangoPoseStatusType.TANGO_POSE_VALID)
        {
            // Create a new Vec3 and Quaternion from the Pose Data received.
            m_tangoPosition[currentIndex] = new Vector3((float)pose.translation [0],
                                                        (float)pose.translation [1],
                                                        (float)pose.translation [2]);
            
            m_tangoRotation[currentIndex] = new Quaternion((float)pose.orientation [0],
                                                           (float)pose.orientation [1],
                                                           (float)pose.orientation [2],
                                                           (float)pose.orientation [3]);
        }
        else // if the current pose is not valid we set the pose to identity
        {
            m_tangoPosition[currentIndex] = Vector3.zero;
            m_tangoRotation[currentIndex] = Quaternion.identity;
            m_isRelocalized = false;
        }
        
        // Reset the current status frame count if the status code changed.
        if (pose.status_code != m_status[currentIndex])
        {
            m_frameCount[currentIndex] = 0;
        }
        
        // Update the stats for the pose for the debug text
        m_status[currentIndex] = pose.status_code;
        m_frameCount[currentIndex]++;
        
        // Compute delta frame timestamp.
        m_frameDeltaTime[currentIndex] = (float)pose.timestamp - m_prevFrameTimestamp[currentIndex];
        m_prevFrameTimestamp [currentIndex] = (float)pose.timestamp;

        Matrix4x4 matrixssTd;
        // If not relocalized MotionTracking pose(Device wrt Start of Service) is used.
        if (!m_isRelocalized) 
        {
            // Construct the device with respect to start of service matrix from the pose.
            matrixssTd = Matrix4x4.TRS(m_tangoPosition[0], m_tangoRotation[0], Vector3.one);
        }
        // If relocalized Device wrt ADF pose is used.
        else 
        {
            // Construct the device with respect to ADF matrix from the pose.
            matrixssTd = Matrix4x4.TRS(m_tangoPosition[1], m_tangoRotation[1], Vector3.one);
        }
        // Converting from Tango coordinate frame to Unity coodinate frame.
        Matrix4x4 matrixuwTuc = m_matrixuwTss * matrixssTd * m_matrixdTuc;
        
        // Extract new local position
        transform.position = matrixuwTuc.GetColumn(3);
        
        // Extract new local rotation
        transform.rotation = Quaternion.LookRotation(matrixuwTuc.GetColumn(2), matrixuwTuc.GetColumn(1));
    }
    
    private void _OnTangoApplicationPermissionsEvent(bool permissionsGranted)
    {
        if(permissionsGranted)
        {
            m_tangoApplication.InitApplication();

            if(m_useADF)
            {
                // Query the full adf list.
                PoseProvider.RefreshADFList();
                // loading last recorded ADF
                string uuid = PoseProvider.GetLatestADFUUID().GetStringDataUUID();
                m_tangoApplication.InitProviders(uuid);
            }
            else
            {
                m_tangoApplication.InitProviders(string.Empty);
            }
            m_tangoApplication.ConnectToService();
        }
        else if (!permissionsGranted)
        {
            AndroidHelper.ShowAndroidToastMessage("Motion Tracking and Area Learning Permissions Needed", true);
        }
    }

    /// <summary>
    /// Unity callback when application is paused.
    /// </summary>
    void OnApplicationPause(bool pauseStatus) {
        m_frameDeltaTime = new float[3];
        m_prevFrameTimestamp = new float[3];
        m_frameCount = new int[3];
        m_status = new TangoEnums.TangoPoseStatusType[3];
        m_tangoRotation = new Quaternion[3];
        m_tangoPosition = new Vector3[3];
    }
}
