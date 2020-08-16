using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityExt.Core;
using UnityEngine.UI;

namespace UnityExt.Core.Examples {

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
                    float t = 200f;
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
                job.speed=new NativeArray<float>(new float[] { 0f,0f,0f },Allocator.TempJob);
                job.angle=new NativeArray<float>(new float[] { 0f,0f,0f },Allocator.TempJob);                                
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
        public enum CaseTypeFlag {
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
            TimerAtomic,
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

        public void Run(CaseTypeFlag p_type) {
            type = p_type;
            if(titleField) titleField.text = "UnityExt / "+type.ToString();
            switch(type) {

                #region Basic
                //Simple activity loop rotating a cube
                case CaseTypeFlag.Basic:  {                    
                    Activity.Run("activity-update",       delegate(Activity a) { Debug.Log("ExampleActivity> Run at Update");       }, Activity.Context.Update);
                    Activity.Run("activity-late-update",  delegate(Activity a) { Debug.Log("ExampleActivity> Run at LateUpdate");   }, Activity.Context.LateUpdate);
                    Activity.Run("activity-fixed-update", delegate(Activity a) { Debug.Log("ExampleActivity> Run at FixedUpdate");  }, Activity.Context.FixedUpdate);
                    Activity.Run("activity-async-update", delegate(Activity a) { Debug.Log("ExampleActivity> Run at Update Async"); }, Activity.Context.Async);
                    Activity.Run("activity-thread",       delegate(Activity a) { Debug.Log("ExampleActivity> Run inside a Thread"); }, Activity.Context.Thread);
                }
                break;
                #endregion

                #region Await
                //Simple activity loop rotating a cube
                case CaseTypeFlag.Await:  {
                    float t = 0f;
                    Activity async_a =
                    Activity.Run("activity-simple-loop",
                    delegate(Activity a) { 
                        t+= Time.deltaTime;
                        if(t>=3f) { Debug.Log("ExampleActivity> Await / Loop Complete"); return false; }
                        return true;
                    }, Activity.Context.Update);
                    //Async callback for the example.
                    System.Action async_cb = 
                    async delegate() { 
                        Debug.Log("ExampleActivity> Await / Activity Wait Start");
                        //Start was tagged as 'async'
                        await async_a;
                        Debug.Log("ExampleActivity> Await / Activity Wait Complete");
                    };
                    //Call the method
                    async_cb();                    
                    Debug.Log("ExampleActivity> Await / After Async Call Break");
                    
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
                    Activity.Run("rotator-activity-example",
                    delegate(Activity a) { 
                        cube_target.transform.localEulerAngles = Vector3.up * rotation_angle;
                        rotation_angle += Time.deltaTime * 90f;
                        return true;
                    }, Activity.Context.Update);
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
                    Activity.Run("rotator-activity-thread",
                    delegate(Activity a) {                         
                        float clk_t = ((float)clk.ElapsedMilliseconds);
                        if(clk_t<1f) return true;
                        dt = clk_t/1000f;
                        rotation_angle += dt * 90f;
                        clk.Restart();
                        return true;
                    }, Activity.Context.Thread);
                    //Starts the loop that collects whichever results are in from the thread
                    Activity.Run("rotator-activity-update",
                    delegate(Activity a) { 
                        cube_target.transform.localEulerAngles = Vector3.up * rotation_angle;
                        return true;
                    }, Activity.Context.Update);
                }
                break;
                #endregion

                #region BasicJob
                //Creates a basic random sum job that perform thousands of operations to emulato app overload.
                case CaseTypeFlag.BasicJob: {                    

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
                    Activity.Run(delegate(Activity a){
                        //Switch the execution pattern while the job executes or not
                        if(Input.GetKeyUp(KeyCode.A)) job_a.context = job_a.context == Activity.Context.JobAsync ? Activity.Context.Job : Activity.Context.JobAsync;
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
                    //Init basic layout data
                    int cx = 19;
                    int cz = 19;
                    int cy = 19;
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
                    Activity.Run(delegate(Activity a) {
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
                            if(k>=max_cubes) break;
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
                }
                break;
                #endregion

                #region TimerBasic
                //Creates a timer running in loop and track the time.
                case CaseTypeFlag.TimerBasic: {
                    //Create an unity-clocked timer, that runs for 3s and counts a step during infinite steps.
                    Timer timer = new Timer("simple-timer",3.0f,0, Timer.Type.Unity);
                    //Start with 3s delay
                    float delay = 3f;
                    timer.Start(delay);
                    //Little UI loop
                    Activity.Run(delegate(Activity a){  
                        
                        ClearLog();
                        Log("=== Simple Timer ===");
                        Log("[A] Restart Timer");
                        Log("[S] Restart Step");
                        Log("[D] Stop");
                        Log("[F] Pause");
                        Log("======");

                        if(Input.GetKeyUp(KeyCode.A)) timer.Restart();
                        if(Input.GetKeyUp(KeyCode.S)) timer.RestartStep();
                        if(Input.GetKeyUp(KeyCode.D)) timer.Stop();
                        if(Input.GetKeyUp(KeyCode.F)) timer.paused = !timer.paused;
                        
                        string t = "#"+timer.step.ToString("00")+" "+timer.elapsed.ToString("0.00")+"s";

                        if(timer.state == Activity.State.Queued)  t = "Waiting "+timer.delay+"s";
                        if(timer.state == Activity.State.Stopped) t = "STOPPED";
                        if(timer.paused) t = "PAUSED / "+t;

                        Log($"<size=23>{t}</size>");

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
                    //Create a thread-clocked timer, that takes 0.15s each step across 3 steps.
                    Timer timer = new Timer("simple-timer",0.15f,3, Timer.Type.System);
                    //Log list
                    List<string> log = new List<string>();
                    //Called each tick.
                    timer.OnExecuteEvent = 
                    delegate(Timer t) {
                        log.Add($"Execute: [{t.step}/{t.count}] [{t.elapsed.ToString("0.00")}/{t.duration.ToString("0.00")}] {t.progress.ToString("0.00")}");
                        if(log.Count>70) log.RemoveAt(0);
                        ClearLog();
                        Log(string.Join("\n",log));
                        return false;
                    };
                    //Called per step
                    timer.OnStepEvent = 
                    delegate(Timer t) {
                        log.Add($"Step: [{t.step}/{t.count}] [{t.elapsed.ToString("0.00")}/{t.duration.ToString("0.00")}] {t.progress.ToString("0.00")}");
                        
                        //Its possible to change the duration anytime, now we randomize it for the next step.
                        if(t.step<t.count) t.duration = Random.Range(0.3f,0.4f);

                        if(log.Count>70) log.RemoveAt(0);
                        ClearLog();
                        Log(string.Join("\n",log));
                        return true;
                    };
                    //Called after the last step.
                    timer.OnCompleteEvent = 
                    delegate(Timer t) {
                        log.Add($"Complete: [{t.step}/{t.count}] [{t.elapsed.ToString("0.00")}/{t.duration.ToString("0.00")}] {t.progress.ToString("0.00")}");
                        if(log.Count>70) log.RemoveAt(0);
                        ClearLog();
                        Log(string.Join("\n",log));
                    };
                    //Starts the timer with 3s of delay.
                    timer.Start(3f);

                }
                break;
                #endregion

                #region TimerAtomic
                //"Atomic" timers uses the system file system to store a time stamp and help keep track of time in the long run execution of the game.
                case CaseTypeFlag.TimerAtomic: {
                    //Clock id
                    string clock_id = "atomic-clock-example";
                    //Init datetimes
                    System.DateTime time_stamp = System.DateTime.UtcNow;                    
                    System.TimeSpan time_span  = new System.TimeSpan(0);
                    //Get or Create an atomic clock by id.
                    time_stamp = Timer.GetAtomicTimestamp(clock_id);
                    time_span  = Timer.GetAtomicClockElapsed(clock_id);
                    
                    //Little UI loop
                    Activity.Run(delegate(Activity a){  
                        
                        ClearLog();
                        Log("=== Simple Atomic Clock ===");
                        Log("[A] Refresh Clock");                        
                        Log("[S] Clear Clock");                        
                        Log(" ");
                        Log("=== Atomic Clock Path ===");
                        Log($"<size=10>{Timer.atomicClockFolder}</size>");

                        bool will_refresh = false;

                        if(Input.GetKeyUp(KeyCode.A)) will_refresh = true;
                        if(Input.GetKeyUp(KeyCode.S)) { Timer.ClearAtomicTimestamp(clock_id); will_refresh=true; }
                        
                        if(will_refresh) {
                            time_stamp = Timer.GetAtomicTimestamp(clock_id);
                            time_span  = Timer.GetAtomicClockElapsed(clock_id);
                        }
                        
                        Log($"<size=14>=== TimeStamp ===</size>");
                        Log(time_stamp.ToString("yy-MM-dd / HH:mm:ss"));
                        Log($"<size=14>=== Elapsed ===</size>");
                        string vh = time_span.TotalHours<=0f ? "" : time_span.TotalHours.ToString("0");
                        string vm = time_span.Minutes.ToString("0");
                        string vs = time_span.Seconds.ToString("0");
                        Log($"{vh}h {vm}min {vs}s");
                        
                        return true;
                    });

                }
                break;
                #endregion

            }
        }

        /// <summary>
        /// CTOR.
        /// </summary>
        protected void Start() {
            ClearLog();
            SetProgress(0f);
            if(type!= CaseTypeFlag.None) Run(type);
        }

        #region Console

        /// <summary>
        /// Clears the log
        /// </summary>
        public void ClearLog() {
            if(!consoleField) return;            
            consoleField.text = "";            
        }

        /// <summary>
        /// Appends a new log.
        /// </summary>
        /// <param name="p_log"></param>
        public void Log(string p_log) {
            if(!consoleField) return;            
            consoleField.text += p_log;
            consoleField.text += "\n";
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