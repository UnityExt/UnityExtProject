using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UI;
using UnityExt.Core;
using BitStream = UnityExt.Core.BitStream;
using System.Security.Cryptography;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine.Profiling;
using System.Linq.Expressions;
using System;
using Random = UnityEngine.Random;

#pragma warning disable CS4014
#pragma warning disable CS1998

namespace UnityExt.Project {

    [System.Serializable]
    public struct Pos3 {
        public float X;
        public float Y;
        public float Z;
    }

    [System.Serializable]
    public struct Coord3 {
        [SerializableField] public Pos3 P0;
        [SerializableField] public Pos3 P1;
        [SerializableField] public Pos3 P2;
    }

    [System.Serializable]
    public class Dataset {
        [SerializableField] public bool     vbool    ;
        [SerializableField] public char     vchar    ;
        [SerializableField] public sbyte    vsbyte   ;
        [SerializableField] public byte     vbyte    ;
        [SerializableField] public short    vshort   ;
        [SerializableField] public ushort   vushort  ;
        [SerializableField] public int      vint     ;
        [SerializableField] public uint     vuint    ;
        [SerializableField] public long     vlong    ;
        [SerializableField] public ulong    vulong   ;
        [SerializableField] public float    vfloat   ;
        [SerializableField] public double   vdouble  ;
        [SerializableField] public decimal  vdecimal ;       
        [SerializableField] public string   vstring  ;
        [SerializableField] public System.DateTime vdate;
        [SerializableField] public System.TimeSpan vtimespan;        
        [SerializableField] public object[] ao;
        [SerializableField] public Pos3 p0;
        [SerializableField] public Coord3  c0;        
        [SerializableField] public object[] l0 = new object[] { 0,1,2,3,null,null,6 };
        [SerializableField] public object[] l1 = new object[] { null,null,null };
        [SerializableField] public object[] l2 = null;
    }


    /// <summary>
    /// Perform a few examples of activity usage.
    /// </summary>
    public class ExamplesApp : MonoBehaviour {

        #region Types

        #region class Rotator
        /// <summary>
        /// Base class that can support different interfaces
        /// </summary>
        public class Rotator : ActivityBehaviour {

            #region struct Rotator.Job

            /// <summary>
            /// Auxiliary struct that also acts as Unity job.
            /// </summary>
            public struct Job : IJob {
                /// <summary>
                /// Speed data
                /// </summary>
                [ReadOnly]
                public NativeArray<float> speed;
                /// <summary>
                /// Angle Data
                /// </summary>                
                public NativeArray<float> angle;
                /// <summary>
                /// Current dt.
                /// </summary>
                public float dt;
                /// <summary>
                /// Runs the job
                /// </summary>
                public void Execute() {
                    float t = 20f;
                    //Super slow stepping
                    for(int i = 0; i<((int)t); i++) {
                        angle[0] += speed[0]*(float)(dt/t);
                        angle[1] += speed[1]*(float)(dt/t);
                        angle[2] += speed[2]*(float)(dt/t);
                    }
                }
            }

            #endregion
            
            /// <summary>
            /// Rotator job
            /// </summary>
            public Job job;

            /// <summary>
            /// Handle for the job.
            /// </summary>
            public JobHandle handle;

            /// <summary>
            /// Flag that tells to schedule the job or not.
            /// </summary>
            public bool schedule;

            /// <summary>
            /// Last time
            /// </summary>
            private double m_last_time;                        
            private Transform m_tcache;
            private bool m_wait;
            private bool m_can_step;
            static System.Diagnostics.Stopwatch m_clk;
            double clk_time { get { return (((double)m_clk.ElapsedMilliseconds)/1000.0);  } }

            /// <summary>
            /// CTOR
            /// </summary>
            virtual protected void Awake() {
                if(m_clk==null) { m_clk = new System.Diagnostics.Stopwatch(); m_clk.Start(); }
                m_last_time = 0f;
                m_tcache    = transform;
                job    = new Job();
                job.speed=new NativeArray<float>(new float[] { 0f,0f,0f },Allocator.Persistent);
                job.angle=new NativeArray<float>(new float[] { 0f,0f,0f },Allocator.Persistent);                                
                handle = default;
                m_can_step = enabled;
            }

            /// <summary>
            /// Set the rotation speed.
            /// </summary>
            /// <param name="v"></param>
            public void SetSpeed(Vector3 v) {
                job.speed[0] = v[0];
                job.speed[1] = v[1];
                job.speed[2] = v[2];
            }

            /// <summary>
            /// Steps the rotation
            /// </summary>
            public void Step() {
                if(!m_can_step) return;
                if(m_wait) return;
                if(schedule)if(!handle.IsCompleted) return;
                double dt = clk_time - m_last_time;                                                                
                job.dt = (float)dt;
                m_last_time = clk_time;
                if(schedule) handle = job.Schedule(); else job.Execute();
                m_wait = true;
            }

            /// <summary>
            /// Applies the rotation.
            /// </summary>
            public void Apply() {  
                //Store flag for threads
                m_can_step = enabled;
                //If not enabled skip
                if(!enabled) return; 
                //If 'schedule' mode and not complete skip otherwise complete the job
                if(schedule) if(!handle.IsCompleted) return; else handle.Complete();
                //Apply results
                if(m_tcache) m_tcache.localEulerAngles = new Vector3(job.angle[0],job.angle[1],job.angle[2]);
                //Signal that the frame is done and new schedule can happen
                m_wait=false;
                //Reset handle
                handle = default;
            }

            /// <summary>
            /// DTOR
            /// </summary>
            override protected void OnDestroy() {
                base.OnDestroy();
                if(schedule) handle.Complete();
                if(job.speed.IsCreated) job.speed.Dispose();
                if(job.angle.IsCreated) job.angle.Dispose();
            }
        }

        /// <summary>
        /// Rotator that runs inside the unity thread
        /// </summary>
        public class MonoRotator : Rotator, IUpdateable {  public void OnUpdate() { Step(); Apply(); } }

        public class JobRotator : Rotator, IUpdateable { override protected void Awake() { base.Awake(); schedule=true; }  public void OnUpdate() { Step(); Apply(); } }

        /// <summary>
        /// Rotator that runs inside a thread and applies the result in the unity thread.
        /// </summary>
        public class ThreadRotator : Rotator, IUpdateable, IThreadUpdateable {  
            public void OnThreadUpdate() { Step();  }
            public void OnUpdate()       { Apply(); } 
        }

        #endregion

        #region struct RandomSumJob

        /// <summary>
        /// Simple job to sum a random scaled number several times.
        /// </summary>
        public struct RandomSumJob : IJob, IJobComponent {
            /// <summary>
            /// Random scale
            /// </summary>
            public float scale;
            /// <summary>
            /// Final Result
            /// </summary>
            public NativeArray<float> result;
            /// <summary>
            /// Auxiliary method to generate random inside jobs
            /// </summary>
            /// <returns></returns>
            public float GetRandom() { return (float)m_random.NextDouble(); }
            static System.Random m_random = new System.Random();
            /// <summary>
            /// Called prior to job execution in the main thread.
            /// </summary>
            public void OnInit() {
                //RandomSumJob jb = this;
                if(!result.IsCreated) result = new NativeArray<float>(1, Allocator.Persistent);
                result[0] = 0f;
                scale = Random.Range(0.01f,0.1f);                
            }
            /// <summary>
            /// Called after job completion sync or async in the main thread.
            /// </summary>
            public void OnComplete() {                
                //Debug.Log(scale+" "+result[0]);
            }
            /// <summary>
            /// Called after activity stop/complete in the main thread.
            /// </summary>
            public void OnDestroy() {                
                if(result.IsCreated) result.Dispose();
            }
            /// <summary>
            /// Exeucte job
            /// </summary>
            public void Execute() {                
                for(int i=0;i<500000;i++) result[0] += GetRandom()*scale;
            }            
        }

        #endregion

        #endregion

        #region enum CaseTypeFlag

        /// <summary>
        /// Enumeration to choose an example.
        /// </summary>
        public enum CaseTypeFlag : ushort {
            None,
            Basic,
            BasicJob,
            Await,
            Rotation,
            RotationThreaded,
            RotationInstancesMono,
            RotationInstancesThreaded,
            RotationInstancesJob,
            TimerBasic,
            TimerSteps,
            TimerAtomicBasic,
            InterpolatorBasic,
            TweenBasic,
            TweenRun,
            TweenAwait,
            BitStreamBasic,
            JsonFileSerialization,
            Base64FileSerialization,
            WebRequestBasic,
            WebRequestPost
        }

        #endregion

        /// <summary>
        /// Example type.
        /// </summary>
        public CaseTypeFlag  type; 

        /// <summary>
        /// Content holder.
        /// </summary>
        public Transform content;

        /// <summary>
        /// Reference to the debug cube.
        /// </summary>
        public GameObject debugCube;

        /// <summary>
        /// Reference to an animation curve for debugging.
        /// </summary>
        public AnimationCurve debugCurve = AnimationCurve.EaseInOut(0f,0f,1f,1f);

        /// <summary>
        /// Reference to the console logging field.
        /// </summary>
        public Text consoleField;

        /// <summary>
        /// Reference to the header title field.
        /// </summary>
        public Text titleField;

        /// <summary>
        /// Reference to the progress bar.
        /// </summary>
        public Image progressBarField;

        /// <summary>
        /// Reference to the progress bar text field.
        /// </summary>
        public Text progressField;

        /// <summary>
        /// Reference of raw image to display texture results
        /// </summary>
        public RawImage imageField;

        /// <summary>
        /// Reference to an image to test.
        /// </summary>
        public Texture2D debugImage;

        /// <summary>
        /// Debug field to check images
        /// </summary>
        public RawImage  debugRawImageUI;

        /// <summary>
        /// Debug field to check images
        /// </summary>
        public Image  debugImageUI;

        /// <summary>
        /// Helper to play audio.
        /// </summary>
        public AudioSource debugAudio;

        /// <summary>
        /// Helper to play with gradients.
        /// </summary>
        public Gradient debugGradient;

        /// <summary>
        /// Internals.
        /// </summary>        
        private List<string>  m_log_lines = new List<string>();

        public void Run(CaseTypeFlag p_type) {
            type = p_type;
            if(titleField) titleField.text = "UnityExt / "+type.ToString();
            switch(type) {

                case CaseTypeFlag.None: {

                }
                break;

                #region Basic
                //Simple activity loop rotating a cube
                case CaseTypeFlag.Basic:  {                    
                    Process.Start("activity-update",       delegate(ProcessContext ctx, Process p) { Debug.Log("ExampleActivity> Run at Update");       return true; }, ProcessContext.Update);
                    Process.Start("activity-late-update",  delegate(ProcessContext ctx, Process p) { Debug.Log("ExampleActivity> Run at LateUpdate");   return true; }, ProcessContext.LateUpdate);
                    Process.Start("activity-fixed-update", delegate(ProcessContext ctx, Process p) { Debug.Log("ExampleActivity> Run at FixedUpdate");  return true; }, ProcessContext.FixedUpdate);
                  //Process.Start("activity-async-update", delegate(ProcessContext ctx, Process p) { Debug.Log("ExampleActivity> Run at Update Async"); return true; }, ProcessContext.Async);
                    Process.Start("activity-thread",       delegate(ProcessContext ctx, Process p) { Debug.Log("ExampleActivity> Run inside a Thread"); return true; }, ProcessContext.Thread);
                }
                break;
                #endregion

                #region BasicJob
                //Creates a basic random sum job that perform thousands of operations to emulato app overload.
                case CaseTypeFlag.BasicJob: {                    
                    /*
                    //Disable VSync to view FPS difference
                    QualitySettings.vSyncCount = 0;

                    //Creates the job and runs it
                    //Watch the 'frameCount' difference between 'sync' and 'async and the FPS as well
                    //Also watch the profiler
                    
                    //List of results logs
                    List<string> job_results = new List<string>();
                    //Creates and executes the job
                    Activity<RandomSumJob> job_a = null;                    
                    job_a =
                    Activity<RandomSumJob>.Run(delegate(Activity n) {
                        Activity<RandomSumJob> a = (Activity<RandomSumJob>)n;
                        job_results.Add("Complete: Frame["+Time.frameCount+"] "+a.context+" scale["+a.job.scale.ToString("0.0")+"] result["+a.job.result[0].ToString("0.0")+"]");
                        if(job_results.Count>10) job_results.RemoveAt(0);
                        return true;
                    },true);
                    //Detects the input and log the status
                    Process.Start(delegate(ProcessContext ctx,Process p){
                        //Switch the execution pattern while the job executes or not
                        if(Input.GetKeyUp(KeyCode.A)) job_a.context = job_a.context == ActivityContext.JobAsync ? ActivityContext.Job : ActivityContext.JobAsync;
                        //Stop the job loop
                        if(Input.GetKeyUp(KeyCode.S)) { job_a.Stop(); }
                        //Start the job loop
                        if(Input.GetKeyUp(KeyCode.D)) { job_a.Start(); job_results.Clear(); }
                        
                        ClearLog();
                        Log($"== Inputs ==");
                        Log($"[A] Switch Job Context");
                        Log($"[S] Stop Job");
                        Log($"[D] Starts Job");
                        Log($"============");
                        Log(string.Join("\n",job_results));
                        ApplyLog();

                        return true;
                    });
                    //*/
                }
                break;
                #endregion

                #region Await
                //Simple activity loop rotating a cube
                case CaseTypeFlag.Await:  {                   
                    float t  = 0f;                    
                    //Create the TaskActivity (basic activity that start runs and stop with success/error
                    TaskActivity async_a = new TaskActivity("activity-simple-loop");
                    //Activity loop
                    async_a.OnExecuteEvent =
                    delegate(TaskActivity a) {
                        switch (a.state) {
                            default: break;
                            case TaskState.Run: {
                                t += a.deltaTime;
                                if (t >= 0.5f) {
                                    Log("Example> Await / Loop Complete");
                                    a.Stop();
                                    return;
                                }
                            }
                            break;
                        }
                        Log($"Example> OnExecuteEvent [{a.context}] state: {a.state} elapsed: {a.elapsed.ToString("0.00")} dt: {a.deltaTime.ToString("0.00")}");
                        ApplyLog();
                    };
                    //Activity event change event from -> to
                    async_a.OnChangeEvent =
                    delegate (TaskActivity a,TaskState sf,TaskState st) {
                        Log($"Example> OnChangeEvent {sf} -> {st}");
                        ApplyLog();
                    };
                    //Adds the activity to the execution pool
                    async_a.Start(ProcessContext.Update);
                    Log("Example> Await / Activity START");
                    ApplyLog();
                    //Async callback for the example.
                    Action async_cb = 
                    async delegate() {
                        Log($"Example> Await / BEFORE 'await' -> Activity Running");
                        ApplyLog();
                        //Start was tagged as 'async'
                        await async_a;
                        Log($"Example> Await / AFTER 'await' -> Activity Finished!");
                        ApplyLog();
                    };
                    //Call the method
                    async_cb();                    
                    
                    
                }
                break;
                #endregion

                #region Rotation
                //Simple activity loop rotating a cube
                case CaseTypeFlag.Rotation:  {
                    GameObject cube_target = Instantiate(debugCube);
                    cube_target.transform.parent = content;
                    cube_target.name = "cube";
                    float rotation_angle = 0f;
                    Process.Start("rotator-activity-example",
                    delegate(ProcessContext ctx, Process p) { 
                        cube_target.transform.localEulerAngles = Vector3.up * rotation_angle;
                        rotation_angle += Time.deltaTime * 90f;
                        return true;
                    });
                }
                break;
                #endregion

                #region RotationThreaded
                //Simple activity loop rotating a cube in a thread and applying current state in the main thread.
                case CaseTypeFlag.RotationThreaded:  {
                    GameObject cube_target = Instantiate(debugCube);
                    cube_target.transform.parent = content;
                    cube_target.name = "cube";
                    float rotation_angle = 0f;                    
                    //Time tracking for threads
                    System.Diagnostics.Stopwatch clk = new System.Diagnostics.Stopwatch();
                    clk.Start();
                    float dt=0f;
                    //Starts the thread to process heavy operation
                    Process.Start("rotator-activity-thread",
                    delegate(ProcessContext ctx, Process p) {                         
                        float clk_t = ((float)clk.ElapsedMilliseconds);
                        if(clk_t<1f) return true;
                        dt = clk_t/1000f;
                        rotation_angle += dt * 90f;
                        clk.Restart();
                        return true;
                    });
                    //Starts the loop that collects whichever results are in from the thread
                    Process.Start("rotator-activity-update",
                    delegate(ProcessContext ctx, Process p) { 
                        cube_target.transform.localEulerAngles = Vector3.up * rotation_angle;
                        return true;
                    });
                }
                break;
                #endregion

                #region RotationInstances / Mono|Threaded|Jobs                
                //This demo instantiates several cubes with a simple rotation component (that performs a simulated heavy operation) that executes in the different contexts allowed by 'Activity'
                case CaseTypeFlag.RotationInstancesJob:
                case CaseTypeFlag.RotationInstancesThreaded:
                case CaseTypeFlag.RotationInstancesMono: {
                    //Disable vsync to see fps.
                    QualitySettings.vSyncCount = 0;
                    //Init basic layout data
                    int cx = 17;
                    int cz = 17;
                    int cy = 18;
                    #if UNITY_WEBGL
                    cx=15;
                    cy=15;
                    cz=15;
                    #endif
                    int max_cubes = cx*cy*cz;
                    float csm = 2f;
                    //cube size
                    float cube_size = (csm-0.8f)/(float)Mathf.Max(cx,cy,cz);
                    //locals
                    Vector3 p = Vector3.zero;
                    Vector3 s = Vector3.one * cube_size;

                    int create_steps = Mathf.Max(1,Mathf.Min(cx,cy,cz));
                    int k=0;

                    List<Rotator> instances = new List<Rotator>();

                    //Async create the layout
                    Process.Start(delegate(ProcessContext ctx,Process pp) {
                        if(k>=max_cubes) {
                            for(int i=0;i<instances.Count;i++) instances[i].enabled=true;
                            return false;
                        }
                        for(int i=0;i<create_steps;i++) {                            
                            //cube grid layout
                            p.x = ((float)(k%cx))      * (cube_size*csm);
                            p.z = ((float)(k/cx)%cz)   * (cube_size*csm);
                            p.y = ((float)(k/(cx*cz))) * (cube_size*csm);

                            p.y -= (((float)(cy-1))*0.55f) * (cube_size*csm);
                        
                            GameObject it = Instantiate(debugCube);
                            it.transform.parent = content;
                            it.transform.localPosition = p;
                            it.transform.localScale    = s;
                            it.name = "cube-"+k.ToString("00000");
                            Rotator rc = null;
                            //Based on example type add the needed rotator
                            if(type==CaseTypeFlag.RotationInstancesMono)     rc = it.AddComponent<MonoRotator>();
                            if(type==CaseTypeFlag.RotationInstancesThreaded) rc = it.AddComponent<ThreadRotator>();
                            if(type==CaseTypeFlag.RotationInstancesJob)      rc = it.AddComponent<JobRotator>();                            
                            //Init random speed
                            if(rc) {                                        
                                rc.SetSpeed(new Vector3(Random.Range(-5f,5f),Random.Range(-40f,40f),Random.Range(-5f,5f)));
                                rc.enabled=false;
                                instances.Add(rc);
                            }
                            k++;
                            float r = Mathf.Clamp01((float)k/(float)max_cubes);
                            SetProgress(r);
                            if(k>=max_cubes) { break; }
                        }                        
                        return true;
                    });
                    Log($"=== Cube Rotation Instances ===");
                    Log($"Each cube instance contains a 'Rotator' component.");
                    Log($"It for-loop 200 steps to rotate and simulate a heavy load.");
                    Log($"The execution falls in one of the contexts offered by 'Activity'.");
                    Log($"=============");
                    Log($"Cubes:{max_cubes}");
                    switch(type) {
                        case CaseTypeFlag.RotationInstancesMono:     Log($"Step: Update");   Log($"Rotate: Update"); break;
                        case CaseTypeFlag.RotationInstancesThreaded: Log($"Step: Thread");   Log($"Rotate: Update"); break;
                        case CaseTypeFlag.RotationInstancesJob:      Log($"Step: UnityJob"); Log($"Rotate: Update"); break;
                    }                    
                    ApplyLog();
                }
                break;
                #endregion

                #region TimerBasic
                //Creates a timer running in loop and track the time.
                case CaseTypeFlag.TimerBasic: {
                    //Create an unity-clocked timer, that runs for 3s and counts a step during infinite steps.
                    Timer timer = new Timer("simple-timer") { duration = 3f,count = 0 } ;
                    //Start with 3s delay
                    float delay = 3f;
                    timer.Start(new TimerStart { delay = delay });
                    //Little UI loop
                    Process.Start(delegate(ProcessContext ctx,Process p){  
                        
                        ClearLog();
                        Log("=== Simple Timer ===");
                        Log("[A] Restart Timer");
                        Log("[S] Restart Step");
                        Log("[D] Stop");
                        Log("[F] Pause");
                        Log("======");

                        if(Input.GetKeyUp(KeyCode.A)) timer.Restart();
                        if(Input.GetKeyUp(KeyCode.S)) timer.step=0;
                        if(Input.GetKeyUp(KeyCode.D)) timer.Stop();
                        if(Input.GetKeyUp(KeyCode.F)) timer.enabled = !timer.enabled;
                        
                        string t = $"#{timer.step.ToString("00")} {timer.time.ToString("0.00")}s / {timer.duration.ToString("0.00")}s | {timer.elapsed.ToString("0.00")}s elapsed";

                        if(timer.state == TimerState.Wait   ) t = "Waiting "+timer.delay+"s";
                        if(timer.state == TimerState.Success) t = "STOPPED";
                        if(!timer.enabled) t = "PAUSED / "+t;

                        Log($"\n<size=20>{t}</size>");

                        ApplyLog();

                        SetProgress(timer.progress);
                        //*/
                        return true;
                    });

                }
                break;
                #endregion

                #region TimerSteps
                //Creates and run a simple timer.
                case CaseTypeFlag.TimerSteps: {
                    //Create a thread-clocked timer, that takes 0.1s each step across 4 steps.
                    Timer timer = new Timer("simple-timer") { duration = 0.2f ,count = 3, delay = 0.2f };
                    //Called each update
                    timer.OnExecuteEvent = 
                    delegate(Timer t) {

                        TimerState s = t.state;

                        string ss = s.ToString().PadRight(10);

                        switch (s) {

                            case TimerState.Wait: {
                                Log($"{ss}: elapsed [{t.elapsed.ToString("0.00")} / {t.delay.ToString("0.00")}]");
                            }
                            break;

                            default: {
                                Log($"{ss}: # {t.step}/{t.count} | t: {t.time.ToString("0.00")} / {t.duration.ToString("0.00")} | step-%: {t.progress.ToString("0.00")} total-%: {t.GetProgress().ToString("0.00")}");
                            }
                            break;
                        }
                        ApplyLog();
                    };
                    //State Change from->to
                    timer.OnChangeEvent =
                    delegate (Timer t,TimerState sf,TimerState st) {
                        Log($"Change: {sf} -> {st} | elapsed [{t.elapsed.ToString("0.00")} / {t.delay.ToString("0.00")}]");
                    };
                    //Starts the timer with delay.
                    timer.Start();

                }
                break;
                #endregion

                #region TimerAtomicBasic
                //"Atomic" timers uses the system file system to store a time stamp and help keep track of time in the long run execution of the game.
                case CaseTypeFlag.TimerAtomicBasic: {
                    //Clock id
                    string clock_id = "atomic-clock-example";
                    //Init datetimes
                    System.DateTime time_stamp = System.DateTime.UtcNow;                    
                    System.TimeSpan time_span  = new System.TimeSpan(0);
                    //Get or Create an atomic clock by id.
                    time_stamp = Timer.GetAtomicTimestamp(clock_id);
                    time_span  = Timer.GetAtomicClockElapsed(clock_id);
                    //Offset seconds to be added to live timer and progress the atomic timestamp in realtime.
                    double offset_seconds = (time_span.TotalMilliseconds/1000.0);
                    //Frame counter to refresh the time stamp
                    int refresh_timeout  = 0;
                    //Local timer counter
                    double t = offset_seconds;
                    //Refresh flag
                    bool will_refresh = true;
                    //Atomic file folder
                    string atomic_clk_folder = Timer.AtomicClockPath;
                    //Little UI loop
                    Process atomic_clock_loop =
                    Process.Start(delegate(ProcessContext ctx,Process p){  
                        
                        ClearLog();
                        Log("=== Simple Atomic Clock ===");
                        Log("Run this demo and try again a few hours later.");
                        Log("===");
                        Log("[A] Refresh Clock");                        
                        Log("[S] Clear Clock");                        
                        Log(" ");
                        Log("=== Atomic Clock Path ===");
                        Log($"<size=10>{atomic_clk_folder}</size>");
                        //*/
                        
                        if(Input.GetKeyUp(KeyCode.A)) will_refresh = true;
                        if(Input.GetKeyUp(KeyCode.S)) { Timer.ClearAtomicTimestamp(clock_id); t = 0; will_refresh=true; }
                        
                        Log($"<size=14>=== TimeStamp ===</size>");
                        Log(time_stamp.ToString("yyyyMMdd / HH:mm:ss"));
                        Log($"<size=14>=== Elapsed (Timer+Timestamp) ===</size>");
                        double dt = offset_seconds-t;
                        //If small delta increment with update dt
                        if(dt<4f) {
                            t+= Time.deltaTime;
                        }
                        //Otherwise interpolate to next offset
                        else {
                            t = t + ((dt) * (Time.deltaTime/4.2f));                        
                        }                        
                        System.TimeSpan ts = System.TimeSpan.FromSeconds(t);                        
                        Log($"{ts.Days.ToString("00")}d {ts.Hours.ToString("00")}h {ts.Minutes.ToString("00")}min {ts.Seconds.ToString("00")}s {ts.Milliseconds.ToString("000")}ms");

                        //Atomic timestamp will build up clock difference to 'real world'
                        //Sync each ~5s
                        //It will snap the value, definite solution would be a bit more harder though.
                        refresh_timeout++;                        
                        if(refresh_timeout>=(60*5)) {
                            refresh_timeout=0;
                            will_refresh = true;                            
                        }

                        if(will_refresh) {
                            time_stamp = Timer.GetAtomicTimestamp(clock_id);
                            time_span  = Timer.GetAtomicClockElapsed(clock_id);
                            offset_seconds = (time_span.TotalMilliseconds/1000.0);
                            will_refresh = false;
                        }

                        ApplyLog();

                        return true;
                    });
                    atomic_clock_loop.name = "atomic-clock-basic";
                }
                break;
                #endregion

                #region Interpolator/Tween Basic                
                //Example showing the basics of the interpolator and tween classes.
                //They can help making easy to mix values and apply these changes of any object and property.
                //Also this demo shows the interconnection between Tweens and Interpolators, where tween automatic animates the properties applying the same steps as manually calling interpolators 'Lerp'
                case CaseTypeFlag.TweenBasic:
                case CaseTypeFlag.InterpolatorBasic: {
                    //Create simple cube
                    GameObject cube_target = Instantiate(debugCube);
                    cube_target.transform.parent = content;
                    cube_target.name = "cube";                    
                    //Duplicate its material
                    MeshRenderer mr = cube_target.GetComponent<MeshRenderer>();
                    //Clone the material for runtime changes
                    string mn = mr.sharedMaterial.name;
                    mr.sharedMaterial = Instantiate(mr.sharedMaterial);
                    mr.sharedMaterial.name = mn;
                    //Create all interpolators
                    PropertyInterpolator<Color>      color_lerp = new PropertyInterpolator<Color>     (mr.sharedMaterial,"_Color",Color.red,Color.green,debugCurve);
                    PropertyInterpolator<Vector3>    pos_lerp   = new PropertyInterpolator<Vector3>   (mr.transform,"position",new Vector3(-1f,0f,0f),new Vector3( 1f,0f,0f),debugCurve);
                    PropertyInterpolator<Quaternion> rot_lerp   = new PropertyInterpolator<Quaternion>(mr.transform,"localRotation",Quaternion.identity,Quaternion.AngleAxis(90f,Vector3.up),debugCurve);                    
                    //Create all tweens setup with 'ids' for cancelling and clamp animation wrapping to stop after completion.
                    Tween<Color>      color_tween = new Tween<Color>     ("tween-b",  mr.sharedMaterial,"_Color"        ) { from = Color.green           , to = Color.red            , duration = 1f, curve  = debugCurve };
                    Tween<Vector3>    pos_tween   = new Tween<Vector3>   ("tween-a",  mr.transform     ,"position"      ) { from = new Vector3(-1f,0f,0f), to = new Vector3(1f,0f,0f), duration = 1f, easing = Tween.Elastic.OutBig };
                    Tween<Quaternion> rot_tween   = new Tween<Quaternion>("tween-b",  mr.transform     ,"localRotation" ) { from = Quaternion.identity   , to = Quaternion.AngleAxis(90f,Vector3.up), duration = 1f, curve = debugCurve };

                    //Angle to increment and apply sin/cos
                    float angle = 0f;
                    //Runs the loop
                    Process.Start("interpolator-example",
                    delegate(ProcessContext ctx, Process p) {

                        ClearLog();
                        Log("=== Tween & Interpolators ===");
                        Log("Try modifying the curve in the inspector.");
                        Log("======");
    
                        switch(type) {

                            #region InterpolatorBasic
                            case CaseTypeFlag.InterpolatorBasic: {
                                Log("Angle: "+angle.ToString("0.0"));
                                angle += Time.deltaTime * 90f;
                                float sin;
                                //Apply interpolations                        
                                sin = Mathf.Sin((angle/5f) * Mathf.Deg2Rad);  sin = (sin+1f)*0.5f;
                                color_lerp.Lerp(sin);
                                sin = Mathf.Sin((angle/10f) * Mathf.Deg2Rad); sin = (sin+1f)*0.5f;
                                pos_lerp.Lerp(sin);
                                sin = Mathf.Sin((angle) * Mathf.Deg2Rad);     sin = (sin+1f)*0.5f;
                                rot_lerp.Lerp(sin);
                            }
                            break;
                            #endregion

                            #region TweenBasic
                            case CaseTypeFlag.TweenBasic: {
                                Log("[Q] Color Tween");
                                Log("[W] Position Tween");
                                Log("[E] Rotation Tween");
                                Log("[A] Speed -0.1");
                                Log("[S] Speed +0.1");
                                Log("[D] Stop");
                                Log("[F] Stop Transforms");
                                Log("[G] Stop Material");
                                Log("[H] Stop Tween A");
                                Log("[J] Stop Tween B");
                                Log("[K] Stop Transform Position");
                                Log("[Z] Wrap Clamp");
                                Log("[X] Wrap Repeat");
                                Log("[C] Wrap Pinpong");
                                Log("[V] Pause");
                                Log("======");
                                Log($"Wrap:     {color_tween.wrap}");
                                Log($"Speed:    {color_tween.speed.ToString("0.0")}x");                                
                                Log($"Paused:   {color_tween.enabled}");
                                Log($"Color:    {color_tween.state.ToString().PadRight(8)} | # {color_tween.step}");
                                Log($"Position: {pos_tween.state.ToString().PadRight(8)}   | # {pos_tween.step  }");
                                Log($"Rotation: {rot_tween.state.ToString().PadRight(8)}   | # {rot_tween.step  }");

                                if(Input.GetKeyDown(KeyCode.Q)) color_tween.Restart();
                                if(Input.GetKeyDown(KeyCode.W)) pos_tween.Restart();
                                if(Input.GetKeyDown(KeyCode.E)) rot_tween.Restart();
                                if(Input.GetKeyDown(KeyCode.A)) { color_tween.speed = pos_tween.speed = (rot_tween.speed -= 0.1f); }
                                if(Input.GetKeyDown(KeyCode.S)) { color_tween.speed = pos_tween.speed = (rot_tween.speed += 0.1f); }
                                
                                if(Input.GetKeyDown(KeyCode.D)) { color_tween.Stop(); pos_tween.Stop(); rot_tween.Stop(); }
                                if(Input.GetKeyDown(KeyCode.F)) { pos_tween.Stop(); rot_tween.Stop(); }
                                if(Input.GetKeyDown(KeyCode.G)) { color_tween.Stop(); }                                
                                //*/
                                if(Input.GetKeyDown(KeyCode.Z)) { color_tween.wrap=pos_tween.wrap=rot_tween.wrap=AnimationWrapMode.Clamp;   }
                                if(Input.GetKeyDown(KeyCode.X)) { color_tween.wrap=pos_tween.wrap=rot_tween.wrap=AnimationWrapMode.Repeat;  }
                                if(Input.GetKeyDown(KeyCode.C)) { color_tween.wrap=pos_tween.wrap=rot_tween.wrap=AnimationWrapMode.Pingpong; }                                
                                if (Input.GetKeyDown(KeyCode.V)) { color_tween.enabled = pos_tween.enabled = (rot_tween.enabled = !rot_tween.enabled); }

                            }
                            break;
                            #endregion

                        }

                        ApplyLog();

                        return true;
                    });

                }
                break;
                #endregion

                #region Tween Run
                //Example showing how to create tweens using the static methods of the class                
                case CaseTypeFlag.TweenRun: {
                    //Create simple cube
                    GameObject cube_target = Instantiate(debugCube);
                    cube_target.transform.parent = content;
                    cube_target.name = "cube";
                    //Locals
                    float speed = 1f;
                    //Tween instance
                    Tween tw = null;
                    //Runs the loop
                    Process.Start("tween-run-example",
                    delegate(ProcessContext ctx, Process p) {

                        ClearLog();
                        Log("=== Tween Run Methods ===");
                        
                        switch(type) {

                            #region TweenRun
                            case CaseTypeFlag.TweenRun: {
                                Log("[Q] Scale Up / 2s Delay");
                                Log("[W] Scale Down / 2s Delay");                                
                                Log("[E] Scale Up");
                                Log("[R] Scale Down");                                
                                Log("[A] Speed -0.025");
                                Log("[S] Speed +0.025");
                                Log("[Z] Stop");                                
                                Log("[X] Pause");
                                Log("======");         
                                Log($"Speed: {speed.ToString("0.00")}x");
                                /*
                                if(Input.GetKeyDown(KeyCode.Q)) {tw = Tween.Run<Vector3>(cube_target.transform,"localScale",Vector3.one*1.5f,0.3f,2f,Tween.Elastic.OutBig);   tw.speed = 1f; }
                                if(Input.GetKeyDown(KeyCode.W)) {tw = Tween.Run<Vector3>(cube_target.transform,"localScale",Vector3.one*0.5f,0.3f,2f,Tween.Elastic.OutSmall); tw.speed = 1f; }
                                if(Input.GetKeyDown(KeyCode.E)) {tw = Tween.Run<Vector3>(cube_target.transform,"localScale",Vector3.one*1.5f,0.3f,0f,Tween.Elastic.OutBig);   tw.speed = speed; }
                                if(Input.GetKeyDown(KeyCode.R)) {tw = Tween.Run<Vector3>(cube_target.transform,"localScale",Vector3.one*0.5f,0.3f,0f,Tween.Elastic.OutSmall); tw.speed = speed; }
                                //*/
                                if(Input.GetKeyDown(KeyCode.A)) { speed -= 0.025f; speed = Mathf.Max(speed,0.025f); }
                                if(Input.GetKeyDown(KeyCode.S)) { speed += 0.025f; speed = Mathf.Max(speed,0.025f); }                                
                                //if(Input.GetKeyDown(KeyCode.Z)) { Tween.Clear(cube_target.transform); }
                                if(Input.GetKeyDown(KeyCode.X)) { if(tw!=null) tw.enabled = !tw.enabled; }
                            }
                            break;
                            #endregion

                        }
                        ApplyLog();
                        return true;
                    });

                }
                break;
                #endregion

                #region TweenAwait

                //Example showing the await/async capabilities
                case CaseTypeFlag.TweenAwait: {
                    //Create simple cube
                    GameObject cube_target = Instantiate(debugCube);
                    cube_target.transform.parent = content;
                    cube_target.name = "cube";                    
                    //Duplicate its material
                    MeshRenderer mr = cube_target.GetComponent<MeshRenderer>();
                    //Clone the material for runtime changes
                    string mn = mr.sharedMaterial.name;
                    mr.sharedMaterial = Instantiate(mr.sharedMaterial);
                    mr.sharedMaterial.name = mn;                    
                    //Create the async callback
                    System.Action run_animation =
                    async 
                    delegate() {                        
                        Debug.Log("ExampleActivity> TweenAwait / Animation Start - Waiting 2s");
                        await Process.Delay(2f); 
                        Debug.Log("ExampleActivity> TweenAwait / Scale Animation 1");
                        //await Tween.Run<Vector3>(cube_target.transform,"localScale",Vector3.one*1.5f,4f,0f,Tween.Elastic.OutBig);                        
                        Debug.Log("ExampleActivity> TweenAwait / Scale Animation 2");
                        //await Tween.Run<Vector3>(cube_target.transform,"localScale",Vector3.one*0.5f,4f,0f,Tween.Elastic.OutSmall);                        
                        Debug.Log("ExampleActivity> TweenAwait / Scale Animation 3");
                        //await Tween.Run<Vector3>(cube_target.transform,"localScale",Vector3.one*1.5f,4f,0f,Tween.Elastic.OutSmall);
                        Debug.Log("ExampleActivity> TweenAwait / Animation Completed!");
                    };
                    run_animation();                    
                    Debug.Log("ExampleActivity> TweenAwait / After Async Call");
                }
                break;

                #endregion

                #region BitStreamBasic
                //Simple BitStream usage
                case CaseTypeFlag.BitStreamBasic: {

                    string fp  = $"{Application.persistentDataPath}/output.txt".Replace("\\","/").Replace("//","/");
                    string zfp = $"{Application.persistentDataPath}/output.txt.z".Replace("\\","/").Replace("//","/");

                    
                    if(File.Exists(fp)) File.Delete(fp);

                    BitStream bs = new BitStream(File.Open(fp, FileMode.CreateNew));

                    bs.Write(45.62f,50f,-30f);
                    bs.Write(11.6f,10f, 16f,3);

                    bs.Flush();

                    bs.BitPosition = 0;

                    float v = 0f;

                    bs.Read(out v,50f,-30f);
                    Debug.Log(v);
                    bs.Read(out v,10f, 16f,3);
                    Debug.Log(v);
                    
                    bs.Close();
                    //*/
                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.RevealInFinder(fp);
                    #endif

                }
                break;
                #endregion

                #region JsonFileSerialization

                //Generates a random filled Dicitonary up to a huge quota
                //Allows saving and loading a json and track profiler behavior
                case CaseTypeFlag.JsonFileSerialization: {

                    Dictionary<string,object> super_data = null;
                    string json_fp = Application.persistentDataPath+"/json.json";
                    
                    Process.Start(
                    delegate(ProcessContext ctx, Process p) { 

                        ClearLog();
                        Log("=== Json Serialization using Files ===");
                        Log("=== Watch the Profiler for GC / Speed ===");
                        Log("[1] Create Data");
                        Log("[2] Save Files Sync");
                        Log("[3] Load Files Sync");                        
                        Log("[4] Save Files Async");
                        Log("[5] Load Files Async");
                        Log("[6] Open Folder");
                        Log("======");
                        ApplyLog();
                        
                        SerializerAttrib sf = SerializerAttrib.None | SerializerAttrib.CloseStream | SerializerAttrib.Safe;

                        string cmd = "";
                        if(Input.GetKeyDown(KeyCode.Alpha1)) cmd = "create-instance";
                        if(Input.GetKeyDown(KeyCode.Alpha2)) cmd = "json-serialize-file";
                        if(Input.GetKeyDown(KeyCode.Alpha3)) cmd = "json-deserialize-file";
                        if(Input.GetKeyDown(KeyCode.Alpha4)) cmd = "json-serialize-file-async";   
                        if(Input.GetKeyDown(KeyCode.Alpha5)) cmd = "json-deserialize-file-async"; 
                        if(Input.GetKeyDown(KeyCode.Alpha6)) cmd = "open-folder"; 
                        
                        if(!string.IsNullOrEmpty(cmd)) Debug.Log($"ExampleApp> Json Serialization / Running {cmd}");

                        switch(cmd) {
                            default: break;
                            #if UNITY_EDITOR
                            case "open-folder": UnityEditor.EditorUtility.RevealInFinder(json_fp); break;
                            #endif
                            case "create-instance": {
                                super_data = new Dictionary<string, object>();
                                Dictionary<string,object> node = super_data;
                                int n=0;
                                int d=0;
                                for(int i=0;i<12000;i++) {
                                    string k = $"field_{i.ToString("000")}";
                                    switch(i%10) {
                                        case 0: case 1: case 2: node[k] = ((long)(Random.value * 0xffffff)).ToString("x"); break;
                                        case 3: case 4: case 5: node[k] = i; break;
                                        case 6: case 7: case 8: int[] l = new int[5]; for(int j=0;j<l.Length;j++)l[j] = n++; node[k] = l; break;
                                        case 9: d++; node["child"] = new Dictionary<string, object>(); node = (Dictionary<string, object>)node["child"]; break;
                                    }
                                }                                
                            }
                            break;

                            case "json-serialize-file-async":
                            case "json-serialize-file": {
                                if(super_data==null) break;
                                bool is_async = cmd.Contains("async");
                                JSONSerializer json_s = new JSONSerializer();
                                System.Action run_jobs =
                                async 
                                delegate() { 
                                    if(is_async) {
                                        await json_s.SerializeAsync(super_data,          json_fp,                 sf); Debug.Log(">> 1");
                                        await json_s.SerializeAsync(super_data,"pass123",json_fp+".pk",           sf); Debug.Log(">> 2");
                                        await json_s.SerializeAsync(super_data,          json_fp+".b64",          sf | SerializerAttrib.Base64); Debug.Log(">> 3");
                                        await json_s.SerializeAsync(super_data,          json_fp+".gzip",         sf | SerializerAttrib.GZip); Debug.Log(">> 4");                  
                                        await json_s.SerializeAsync(super_data,          json_fp+".b64.gzip",     sf | SerializerAttrib.GZip | SerializerAttrib.Base64); Debug.Log(">> 5");
                                        await json_s.SerializeAsync(super_data,"pass123",json_fp+".b64.gzip.pk",  sf | SerializerAttrib.GZip | SerializerAttrib.Base64); Debug.Log(">> 6");
                                    }
                                    else {
                                        json_s.Serialize(super_data,          json_fp);
                                        json_s.Serialize(super_data,"pass123",json_fp+".pk");
                                        json_s.Serialize(super_data,          json_fp+".b64",          sf | SerializerAttrib.Base64);                                    
                                        json_s.Serialize(super_data,          json_fp+".gzip",         sf | SerializerAttrib.GZip);                                    
                                        json_s.Serialize(super_data,          json_fp+".b64.gzip",     sf | SerializerAttrib.GZip | SerializerAttrib.Base64);  
                                        json_s.Serialize(super_data,"pass123",json_fp+".b64.gzip.pk",  sf | SerializerAttrib.GZip | SerializerAttrib.Base64);                                    
                                    }                                     
                                };
                                run_jobs();
                            }
                            break;

                            case "json-deserialize-file-async":
                            case "json-deserialize-file": {           
                                JSONSerializer json_s = new JSONSerializer();
                                bool is_async = cmd.Contains("async");
                                System.Action run_jobs =
                                async 
                                delegate() {                                                                                                            
                                    Dictionary<string,object> d;
                                    if(is_async) {
                                        await json_s.DeserializeAsync<Dictionary<string,object>>(          json_fp);                                                                       Debug.Log(json_s.GetResult<Dictionary<string,object>>());
                                        await json_s.DeserializeAsync<Dictionary<string,object>>("pass123",json_fp+".pk");                                                                 Debug.Log(json_s.GetResult<Dictionary<string,object>>());
                                        await json_s.DeserializeAsync<Dictionary<string,object>>(          json_fp+".b64",          sf | SerializerAttrib.Base64);                         Debug.Log(json_s.GetResult<Dictionary<string,object>>());          
                                        await json_s.DeserializeAsync<Dictionary<string,object>>(          json_fp+".gzip",         sf | SerializerAttrib.GZip);                           Debug.Log(json_s.GetResult<Dictionary<string,object>>());        
                                        await json_s.DeserializeAsync<Dictionary<string,object>>(          json_fp+".b64.gzip",     sf | SerializerAttrib.GZip | SerializerAttrib.Base64); Debug.Log(json_s.GetResult<Dictionary<string,object>>());
                                        await json_s.DeserializeAsync<Dictionary<string,object>>("pass123",json_fp+".b64.gzip.pk",  sf | SerializerAttrib.GZip | SerializerAttrib.Base64); Debug.Log(json_s.GetResult<Dictionary<string,object>>());
                                    }
                                    else {
                                        d = json_s.Deserialize<Dictionary<string,object>>(          json_fp);
                                        d = json_s.Deserialize<Dictionary<string,object>>("pass123",json_fp+".pk");
                                        d = json_s.Deserialize<Dictionary<string,object>>(          json_fp+".b64",          sf | SerializerAttrib.Base64);                                    
                                        d = json_s.Deserialize<Dictionary<string,object>>(          json_fp+".gzip",         sf | SerializerAttrib.GZip);                                    
                                        d = json_s.Deserialize<Dictionary<string,object>>(          json_fp+".b64.gzip",     sf | SerializerAttrib.GZip | SerializerAttrib.Base64);  
                                        d = json_s.Deserialize<Dictionary<string,object>>("pass123",json_fp+".b64.gzip.pk",  sf | SerializerAttrib.GZip | SerializerAttrib.Base64);                                    
                                    }                                     
                                };
                                run_jobs();
                            }
                            break;
                        }
                        return true;
                    });

                }
                break;

                #endregion

                #region Base64FileSerialization
                //Simple serialization of string and bytes into Base64
                case CaseTypeFlag.Base64FileSerialization: {

                    Base64Serializer b64s = new Base64Serializer();
                    object b64s_output;

                    string str_d = "Some Text for Base64";
                    byte[] bin_d = new byte[] { 65,66,67,68,69,70,71,72,73,74,75 };

                    string str_fp = Application.persistentDataPath+"/txt64.b64";
                    string bin_fp = Application.persistentDataPath+"/bin64.b64";

                    StringBuilder sb = new StringBuilder();

                    b64s.Serialize(str_d,          str_fp           , SerializerAttrib.CloseStream);
                    b64s.Serialize(str_d,          str_fp+".gzip"   , SerializerAttrib.CloseStream | SerializerAttrib.GZip);
                    b64s.Serialize(str_d,"pass123",str_fp+".pk"     , SerializerAttrib.CloseStream);
                    b64s.Serialize(str_d,"pass123",str_fp+".gzip.pk", SerializerAttrib.CloseStream | SerializerAttrib.GZip);
                    b64s.Serialize(str_d,          sb               , SerializerAttrib.CloseStream);

                    Debug.Log($"Base64: {sb}");

                    b64s_output = b64s.Deserialize<string>(          str_fp,            SerializerAttrib.CloseStream);                         Debug.Log(b64s_output);
                    b64s_output = b64s.Deserialize<string>(          str_fp+".gzip",    SerializerAttrib.CloseStream | SerializerAttrib.GZip); Debug.Log(b64s_output);
                    b64s_output = b64s.Deserialize<string>("pass123",str_fp+".pk",      SerializerAttrib.CloseStream);                         Debug.Log(b64s_output);
                    b64s_output = b64s.Deserialize<string>("pass123",str_fp+".gzip.pk", SerializerAttrib.CloseStream | SerializerAttrib.GZip); Debug.Log(b64s_output);
                    b64s_output = b64s.Deserialize<string>(sb,                          SerializerAttrib.CloseStream);                         Debug.Log(b64s_output);

                    b64s.Serialize(bin_d,          bin_fp           , SerializerAttrib.CloseStream);
                    b64s.Serialize(bin_d,          bin_fp+".gzip"   , SerializerAttrib.CloseStream | SerializerAttrib.GZip);
                    b64s.Serialize(bin_d,"pass123",bin_fp+".pk"     , SerializerAttrib.CloseStream);
                    b64s.Serialize(bin_d,"pass123",bin_fp+".gzip.pk", SerializerAttrib.CloseStream | SerializerAttrib.GZip);
                    b64s.Serialize(bin_d,          sb               , SerializerAttrib.CloseStream);

                    Debug.Log($"Base64: {sb}");

                    b64s_output = b64s.Deserialize<byte[]>(          bin_fp,            SerializerAttrib.CloseStream);                         Debug.Log(string.Join(",",(byte[])b64s_output));
                    b64s_output = b64s.Deserialize<byte[]>(          bin_fp+".gzip",    SerializerAttrib.CloseStream | SerializerAttrib.GZip); Debug.Log(string.Join(",",(byte[])b64s_output));
                    b64s_output = b64s.Deserialize<byte[]>("pass123",bin_fp+".pk",      SerializerAttrib.CloseStream);                         Debug.Log(string.Join(",",(byte[])b64s_output));
                    b64s_output = b64s.Deserialize<byte[]>("pass123",bin_fp+".gzip.pk", SerializerAttrib.CloseStream | SerializerAttrib.GZip); Debug.Log(string.Join(",",(byte[])b64s_output));
                    b64s_output = b64s.Deserialize<byte[]>(sb,                          SerializerAttrib.CloseStream);                         Debug.Log(string.Join(",",(byte[])b64s_output));

                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.RevealInFinder(str_fp);
                    #endif

                }
                break;

                #endregion

                #region WebRequestBasic
                case CaseTypeFlag.WebRequestBasic: {
                    //Init the Data File System
                    WebRequest.InitDataFileSystem();
                    //Clear any existing cache (disabled to show caching features)
                    //WebRequestCache.Clear();
                    //Create the WebRequest
                    WebRequest req = new WebRequest("image-loader") {
                        ttl     = 0.5f, //Cache TimeToLive in Minutes (15s)
                        timeout =   10f //Timeout in seconds
                    };
                    ///WebRequest execution loop with extensive states for tracking
                    req.OnExecuteEvent =
                    delegate (WebRequest p_request) {
                        //Ignore Processing as its just looping waiting for other mid steps
                        if (p_request.state == WebRequestState.Processing) return;
                        //Request Progress == Upload
                        float pu = p_request.request  == null ? 0f : p_request.request.progress;
                        //Response Progress == Download
                        float pd = p_request.response == null ? 0f : p_request.response.progress;
                        Log($"State: {p_request.state} | d: {pd.ToString("0.00")} u: {pu.ToString("0.00")}");
                        switch (p_request.state) {

                            case WebRequestState.Create: {
                                Log($"  URL: [{p_request.url}]");
                            }
                            break;

                            case WebRequestState.DownloadProgress: {
                                SetProgress(p_request.progress);
                            }
                            break;
                            case WebRequestState.UploadProgress: {
                                SetProgress(p_request.progress);
                            }
                            break;

                            case WebRequestState.Success: {
                                Log($"  CACHED: {p_request.cached}\n  URL: {p_request.GetURL()}\n  URL FULL: {p_request.GetURL(true)}");                                                                                              
                                if (imageField.texture) Destroy(imageField.texture);
                                Texture2D img = p_request.GetTexture();
                                int tw = img ? img.width  : 0;
                                int th = img ? img.height : 0;
                                float scl = 1024f / (float)img.width;
                                int iw = scl <= 0f ? 0 : (int)(tw * scl);
                                int ih = scl <= 0f ? 0 : (int)(th * scl);
                                imageField.rectTransform.sizeDelta = new Vector2(iw, ih);
                                imageField.texture = img;                                                                
                            }
                            break;

                            case WebRequestState.Error: {
                                Log($"  ERROR:{p_request.exception}");
                            }
                            break;
                        }                        
                    };
                    //Loop to refresh logs
                    Process.Start(delegate (ProcessContext p_ctx,Process p_proc) { ApplyLog(); return true; },ProcessContext.Update);
                    //Configuration flags
                    WebRequestFlags req_flags = WebRequestFlags.FileBuffer | WebRequestFlags.FileCache; //Buffer and Cache Policies (can be using RAM or Disk)
                    //Perform a GET operation of a big image
                    req.Get("https://edmullen.net/test/rc.jpg",req_flags);
                }
                break;
                #endregion

                case CaseTypeFlag.WebRequestPost: {
                    //Init the Data File System
                    WebRequest.InitDataFileSystem();
                    //Clear any existing cache (disabled to show caching features)
                    //WebRequestCache.Clear();
                    //Create the WebRequest
                    WebRequest req = new WebRequest("image-loader") {
                        ttl     = 0.5f, //Cache TimeToLive in Minutes (15s)
                        timeout = 10f   //Timeout in seconds
                    };
                    //Populate the Request Body with lots of supported data
                    req.request.body.AddField ("str","some-text");
                    req.request.body.AddBase64("json-b64","{ a: 1, b: 2}");
                    req.request.body.AddBase64("bin-b64",new byte[] { 0,1,2,3,4,5 });
                    req.request.body.AddFields("table",new Dictionary<string,object>() { { "a",1 },{ "b","txt" },{ "c",2.345f } });
                    req.request.body.AddFields("list",new object[] { "a",1,"b","txt","c",2.345f });
                    req.request.body.AddField ("","list-noidx","0");
                    req.request.body.AddField ("","list-noidx","1");
                    req.request.body.AddField ("","list-noidx","2");
                    req.request.body.AddField ("","list-noidx","3");
                    req.request.body.AddJson  ("json-obj",new Dictionary<string,object>() { { "a",1 },{ "b","txt" },{ "c",2.345f } });
                    req.request.body.AddJson  ("json-arr",new object[] { "a",1,"b","txt","c",2.345f });
                    req.request.body.AddPNG   ("img-png",debugImage);
                    req.request.body.AddJPEG  ("img-jpg",debugImage);
                    ///WebRequest execution loop with extensive states for tracking
                    req.OnExecuteEvent =
                    delegate (WebRequest p_request) {
                        //Ignore Processing as its just looping waiting for other mid steps
                        if (p_request.state == WebRequestState.Processing) return;
                        //Request Progress == Upload
                        float pu = p_request.request == null ? 0f : p_request.request.progress;
                        //Response Progress == Download
                        float pd = p_request.response == null ? 0f : p_request.response.progress;
                        Log($"State: {p_request.state} | d: {pd.ToString("0.00")} u: {pu.ToString("0.00")}");
                        switch (p_request.state) {

                            case WebRequestState.Create: {
                                Log($"  URL: [{p_request.url}]");
                            }
                            break;

                            case WebRequestState.DownloadProgress: {
                                SetProgress(p_request.progress);
                            }
                            break;
                            case WebRequestState.UploadProgress: {
                                SetProgress(p_request.progress);
                            }
                            break;

                            case WebRequestState.Success: {
                                Log($"=== PAYLOAD ===");
                                Log(p_request.GetString("<null>"));
                                Log($"===============");
                            }
                            break;

                            case WebRequestState.Error: {
                                Log($"  ERROR:{p_request.exception}");
                            }
                            break;
                        }
                    };
                    //Loop to refresh logs
                    Process.Start(delegate (ProcessContext p_ctx,Process p_proc) { ApplyLog(); return true; },ProcessContext.Update);
                    //Configuration flags
                    WebRequestFlags req_flags = WebRequestFlags.FileBuffer | WebRequestFlags.FileCache; //Buffer and Cache Policies (can be using RAM or Disk)
                    //Perform a POST operation and track the result in the 'request catcher' tool
                    req.Post("https://unityex.requestcatcher.com/",req_flags);
                }
                break;

            }
        }

        /// <summary>
        /// CTOR.
        /// </summary>
        protected void Start() {            
            ClearLog();
            SetProgress(0f);
            Run(type);
        }

        #region Console

        /// <summary>
        /// Clears the log
        /// </summary>
        public void ClearLog() {
            if(!consoleField) return;               
            consoleField.text = "";
            m_log_lines.Clear();            
        }

        /// <summary>
        /// Appends a new log.
        /// </summary>
        /// <param name="p_log"></param>
        public void Log(string p_log) {
            if(!consoleField) return;
            lock (m_log_lines) {
                m_log_lines.Add(p_log);
                while (m_log_lines.Count > 90) m_log_lines.RemoveAt(0);
            }
        }

        /// <summary>
        /// Applies the written log.
        /// </summary>
        public void ApplyLog() {
            if(!consoleField) return;
            lock (m_log_lines) {
                consoleField.text = string.Join("\n",m_log_lines);
            }
        }

        /// <summary>
        /// Set the progress bar in the bottom.
        /// </summary>
        /// <param name="p_progress"></param>
        public void SetProgress(float p_progress) {
            progressBarField.rectTransform.localScale = new Vector3(Mathf.Clamp01(p_progress),1f,1f);
            progressField.text = "Progress: "+Mathf.FloorToInt(p_progress*100f)+"%";
            progressField.enabled = p_progress>0f;
        }

        #endregion

    }

}