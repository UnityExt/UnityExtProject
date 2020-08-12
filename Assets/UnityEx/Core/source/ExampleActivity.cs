using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityExt.Core;

namespace UnityExt.Core.Examples {

    /// <summary>
    /// Perform a few examples of activity usage.
    /// </summary>
    public class ExampleActivity : MonoBehaviour {

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
                    float t = 1f;
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
                if(!result.IsCreated) result = new NativeArray<float>(1, Allocator.TempJob);
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
            Basic,
            BasicJob,
            Await,
            Rotation,
            RotationThreaded,
            RotationBatch,
            RotationBatchThreaded,
            RotationBatchJob,
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
        /// CTOR.
        /// </summary>
        protected void Start() {

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
                    Debug.Log("ExampleActivity> Await / Before Break");
                    
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

                case CaseTypeFlag.BasicJob: {                    
                    //Creates the job and runs it
                    //Watch the 'frameCount' difference between 'sync' and 'async and the FPS as well
                    //Also watch the profiler
                    Activity<RandomSumJob> job_a = null;
                    job_a =
                    Activity<RandomSumJob>.Run(delegate(Activity n) {
                        Activity<RandomSumJob> a = (Activity<RandomSumJob>)n;
                        Debug.Log(Time.frameCount+" "+a.context+" "+a.job.scale+" "+a.job.result[0]);                        
                        return true;
                    },true);

                    float c_frame = 0f;
                    float c_time  = 0f;

                    //Small FPS counter and input detection running in Mono.Update
                    Activity.Run(delegate(Activity a){
                        //Switch the execution pattern while the job executes or not
                        if(Input.GetKeyUp(KeyCode.A)) job_a.context = job_a.context == Activity.Context.JobAsync ? Activity.Context.Job : Activity.Context.JobAsync;
                        //Stop the job loop
                        if(Input.GetKeyUp(KeyCode.S)) job_a.Stop();
                        //Start the job loop
                        if(Input.GetKeyUp(KeyCode.D)) job_a.Start();
                        //FPS counter
                        c_frame += 1f;
                        c_time  += Time.deltaTime;
                        if(c_time<0.5f) return true;                        
                        Debug.Log("FPS: "+(c_frame*2f).ToString("0"));
                        c_time  = 0f;
                        c_frame = 0f;
                        return true;
                    });
                    
                }
                break;

                #endregion

                #region RotationBatch / Mono|Threaded|Jobs
                case CaseTypeFlag.RotationBatchJob:
                case CaseTypeFlag.RotationBatchThreaded:
                case CaseTypeFlag.RotationBatch: {

                    //Init basic layout data
                    int cx = 20;
                    int cz = 20;
                    int cy = 20;
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

                            p.y -= (((float)(cy-1))*0.5f)  * (cube_size*csm);
                        
                            GameObject it = Instantiate(debugCube);
                            it.transform.parent = content;
                            it.transform.localPosition = p;
                            it.transform.localScale    = s;
                            it.name = "cube-"+k.ToString("00000");
                            Rotator rc = null;
                            //Based on example type add the needed rotator
                            if(type==CaseTypeFlag.RotationBatch)         rc = it.AddComponent<MonoRotator>();
                            if(type==CaseTypeFlag.RotationBatchThreaded) rc = it.AddComponent<ThreadRotator>();
                            if(type==CaseTypeFlag.RotationBatchJob)      rc = it.AddComponent<JobRotator>();                            
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

                    Debug.Log($"ExampleActivity> Created [{max_cubes}] Cubes");

                }
                break;
                #endregion

            }

        }


    }

}