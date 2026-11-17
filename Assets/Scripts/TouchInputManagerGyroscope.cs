using CyberBiker.Player;
using Gameplay;
using UnityEngine;

public partial class TouchInputManager {
    [Header("Mobile steering")]
    [SerializeField] private MobileSteeringMode steeringMode=MobileSteeringMode.Swipe;
    [SerializeField,Range(.25f,3)] private float gyroSensitivity=1;
    [SerializeField,Range(0,5)] private float gyroDeadZone=2;
    [SerializeField,Range(0,.4f)] private float gyroSmoothing=.12f;
    [SerializeField] private bool gyroInvert;
    private readonly TiltSteering tilt=new TiltSteering();
    private bool gyroHasFocus=true,gyroAppPaused,ownsGyro,previousGyroEnabled,hasOrientation;
    private float previousGyroInterval,gyroSteering;
    private Gyroscope gyroscope;
    private ScreenOrientation sensorOrientation;
    private bool sensorCompensated;
    public MobileSteeringMode SteeringMode=>steeringMode;
    public float GyroSensitivity=>gyroSensitivity;
    public float GyroDeadZone=>gyroDeadZone;
    public float GyroSmoothing=>gyroSmoothing;
    public bool GyroInverted=>gyroInvert;
    public bool GyroscopeCalibrated=>tilt.Calibrated;
    private bool SupportsGyro=>SystemInfo.supportsGyroscope;
    private bool SupportsTilt=>SystemInfo.supportsGyroscope || SystemInfo.supportsAccelerometer;
    public bool UsesGyroscopeSteering=>steeringMode==MobileSteeringMode.Gyroscope && TiltAvailable;
    private bool TiltAvailable {
        get {
#if UNITY_EDITOR
            if(testTiltEnabled)return testTiltSupported;
#endif
            return SupportsTilt;
        }
    }
    public string GyroscopeStatus=>!TiltAvailable ? "No tilt sensor detected. Swipe and keyboard controls remain available." : !tilt.Calibrated && UsesGyroscopeSteering ? "Hold the phone facing you in your normal riding position to calibrate." : SupportsGyro ? "Gyroscope available. Hold comfortably, then APPLY to switch controls." : "Using accelerometer tilt. Hold comfortably, then APPLY to switch controls.";
    public void ConfigureGyroscope(MobileSteeringMode mode,float sensitivity,float deadZone,float smoothing,bool invert){
        bool changed=steeringMode!=mode;
        steeringMode=mode;gyroSensitivity=Mathf.Clamp(sensitivity,.25f,3);gyroDeadZone=Mathf.Clamp(deadZone,0,5);gyroSmoothing=Mathf.Clamp(smoothing,0,.4f);gyroInvert=invert;
        if(changed || !UsesGyroscopeSteering){drag=delta=Vector2.zero;leftPressed=rightPressed=false;ClearTapPointers();RequestGyroscopeCalibration();}
        SyncGyroscope();ApplyDesktopTapArea();
    }
    public void RequestGyroscopeCalibration(){tilt.ResetCalibration();gyroSteering=0;}
    private void SyncGyroscope(){
        bool wanted=steeringMode==MobileSteeringMode.Gyroscope && gyroHasFocus && !gyroAppPaused && SupportsGyro && isActiveAndEnabled;
#if UNITY_EDITOR
        if(testTiltEnabled)wanted=false;
#endif
        if(wanted && !ownsGyro){gyroscope=Input.gyro;previousGyroEnabled=gyroscope.enabled;previousGyroInterval=gyroscope.updateInterval;gyroscope.enabled=true;gyroscope.updateInterval=1f/60;ownsGyro=true;}
        else if(!wanted && ownsGyro)ReleaseGyroscope();
    }
    private void ReleaseGyroscope(){if(ownsGyro && gyroscope!=null){gyroscope.updateInterval=previousGyroInterval;gyroscope.enabled=previousGyroEnabled;}ownsGyro=false;}
    private void UpdateGyroscopeInput(){
        SyncGyroscope();
        if(!UsesGyroscopeSteering || !gyroHasFocus || gyroAppPaused){gyroSteering=0;tilt.ResetOutput();return;}
        var orientation=Screen.orientation;bool compensated=Input.compensateSensors;
        Vector3 gravity=SupportsGyro && gyroscope!=null ? gyroscope.gravity : Input.acceleration;
#if UNITY_EDITOR
        if(testTiltEnabled){gravity=testTiltGravity;orientation=testTiltOrientation;compensated=testTiltCompensated;}
#endif
        if(!hasOrientation || sensorOrientation!=orientation || sensorCompensated!=compensated){sensorOrientation=orientation;sensorCompensated=compensated;hasOrientation=true;RequestGyroscopeCalibration();}
        if(TiltSteering.TryReadRoll(gravity,orientation,compensated,out float angle))gyroSteering=tilt.Sample(angle,gyroSensitivity,gyroDeadZone,gyroSmoothing,gyroInvert,Time.unscaledDeltaTime);
        else {gyroSteering=0;tilt.ResetOutput();}
        if(gm!=null && gm.gameState!=GameState.Playing){gyroSteering=0;tilt.ResetOutput();}
    }
    private void SuspendGyroscope(){ReleaseGyroscope();gyroSteering=0;tilt.ResetOutput();drag=delta=Vector2.zero;leftPressed=rightPressed=false;}
    private void OnEnable(){gyroHasFocus=true;gyroAppPaused=false;hasOrientation=false;RequestGyroscopeCalibration();}
    private void OnApplicationPause(bool paused){gyroAppPaused=paused;if(paused)SuspendGyroscope();else RequestGyroscopeCalibration();}
    private void OnApplicationFocus(bool focused){gyroHasFocus=focused;if(!focused)SuspendGyroscope();else RequestGyroscopeCalibration();}
#if UNITY_EDITOR
    private bool testTiltEnabled,testTiltSupported,testTiltCompensated;
    private Vector3 testTiltGravity;
    private ScreenOrientation testTiltOrientation;
    public void SetGyroscopeTestSample(Vector3 gravity,bool supported=true,ScreenOrientation orientation=ScreenOrientation.LandscapeLeft,bool compensated=true){
        if(!testTiltEnabled){gyroHasFocus=true;gyroAppPaused=false;}
        testTiltEnabled=true;testTiltSupported=supported;testTiltGravity=gravity;testTiltOrientation=orientation;testTiltCompensated=compensated;ApplyDesktopTapArea();
    }
#endif
}
