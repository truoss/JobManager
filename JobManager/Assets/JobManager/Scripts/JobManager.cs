//#define DEBUG
using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

namespace JobManagerSpace
{
    public class JobManager : MonoBehaviour
    {
        public static JobManager I;
        public bool checkDuplicate = true;

        void Awake()
        {
            I = this;
        }

        public static bool isWorking { get { return jobs.Count > 0; } }

        static readonly Stack<Job> jobCache = new Stack<Job>();
        static List<Job> jobs = new List<Job>();
        
        public static Job NewJob(string name, Task[] tasks)
        {
            return NewJob(name, tasks, null, null, null);
        }

        public static Job NewJob(string name, Task[] tasks, Action callbackFinished, Action callbackStopped, Action callbackFailed)
        {
            if (jobCache.Count > 0)
            {
                var job = jobCache.Pop();
                job.state = Job.JobStates.Created;
                if (tasks != null)
                    job.SetTasks(tasks);
                job.name = name;
                job.OnFinished = callbackFinished;
                job.OnStopped = callbackStopped;
                job.OnFailed = callbackFailed;

                return job;
            }
            else
                return new Job(name, tasks, callbackFinished, callbackStopped, callbackFailed);
        }

        public bool AddJob(Job job)
        {
            if (ValidateJob(job))
            {
                jobs.Add(job);
                return true;
            }
            else
                return false;
        }

        private bool ValidateJob(Job job)
        {
            if (checkDuplicate)
            {
                if (isDublicate(job))
                    return false;
            }

            return job.ValidateJob();
        }

        void Update()
        {
            if (jobs.Count == 0)
                return;

            //(backwards since we will be modifying the list)
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                if (jobs[i].state == Job.JobStates.Created)
                {
                    StartCoroutine(jobs[i].Run());
                }
                else
                {
                    if (jobs[i].state == Job.JobStates.Done)
                    {
                        jobCache.Push(jobs[i]);
                        jobs.RemoveAt(i);
                    }
                }
            }
        }

        public bool isDublicate(Job job)
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                if (jobs[i].name == job.name)
                {
                    return true;                   
                }
            }

            return false;
        }

        #region DEBUG
        [ContextMenu("Debug Jobs")]
        void DebugJobs()
        {
            Debug.LogWarning("JobCache.Count: " + jobCache.Count);

            for (int i = 0; i < jobs.Count; i++)
            {
                Debug.LogWarning(jobs[i].PrintDebug());
            }
        }
        #endregion
    }

    public class Job
    {
        public string name = "Job";

        public enum JobStates
        {
            Created,
            Running,
            Stopped,
            Failed,
            Done,
            Unknown
        }
        public JobStates state = JobStates.Unknown;
        public Action OnFinished;
        public Action OnFailed;
        public Action OnStopped;
        int curTaskIdx = 0;

        DateTime startTime;
        DateTime startTaskTime;
        int startFrame;
        int startTaskFrame;

        static readonly Stack<Task> taskCache = new Stack<Task>();
        Task[] tasks;

        public bool testCondition = false;

        public static Task NewTask(Action action)
        {
            if (taskCache.Count > 0)
            {
                var task = taskCache.Pop();
                task.type = Task.TaskType.Action;
                task.action = action;
                task.condition = null;
                task.time = 0f;

                return task;
            }
            else
                return new Task(action);
        }

        public static Task NewTask(Func<bool> condition)
        {
            if (taskCache.Count > 0)
            {
                var task = taskCache.Pop();
                task.type = Task.TaskType.Condition;
                task.action = null;
                task.condition = condition;
                task.time = 0f;

                return task;
            }
            else
                return new Task(condition);
        }

        public static Task NewTask(float time)
        {
            if (taskCache.Count > 0)
            {
                var task = taskCache.Pop();
                task.type = Task.TaskType.Timer;
                task.action = null;
                task.condition = null;
                task.time = time;

                return task;
            }
            else
                return new Task(time);
        }

        public bool ValidateJob()
        {
            if (state != JobStates.Created || name == "" || tasks == null || tasks.Length == 0)
            {
                Debug.LogWarning("Job is invalid!");
                return false;
            }
            else
                return true;
        }

        public Job(string name)
        {
            state = JobStates.Created;

            // set parameter
            this.name = name;

            OnFailed = null;
            OnFinished = null;
            OnStopped = null;
        }

        public Job(string name, Task[] tasks)
        {
            state = JobStates.Created;

            // set parameter
            this.name = name;
            this.tasks = tasks;

            OnFailed = null;
            OnFinished = null;
            OnStopped = null;
        }

        public Job(string name, Task[] tasks, Action callbackFinished, Action callbackStopped, Action callbackFailed)
        {
            state = JobStates.Created;

            // set parameter
            this.name = name;
            this.tasks = tasks;

            OnFailed = callbackFinished;
            OnFinished = callbackStopped;
            OnStopped = callbackFailed;
        }

        public void SetTasks(Task[] tasks)
        {
            this.tasks = tasks;
        }

        public IEnumerator Run()
        {
            // Check input
            if (name == "")
            { yield break; }

            if (tasks == null || tasks.Length == 0)
            { yield break; }

            if (state == JobStates.Done || state == JobStates.Running)
            { yield break; }

            if (state == JobStates.Failed)
            { Failed(0); yield break; }

            if (state == JobStates.Stopped)
            { Stopped(0); yield break; }

            startTime = DateTime.Now;
            startFrame = Time.frameCount;
            state = JobStates.Running;

            for (curTaskIdx = 0; curTaskIdx < tasks.Length; curTaskIdx++)
            {
                if (state == JobStates.Failed)
                {
                    Failed(curTaskIdx);
                    yield break;
                }

                if (state == JobStates.Stopped)
                {
                    Stopped(curTaskIdx);
                    yield break;
                }
#if DEBUG
                startTaskTime = DateTime.Now;
                startTaskFrame = Time.frameCount;
#endif

                if (tasks[curTaskIdx] != null)
                {
                    switch (tasks[curTaskIdx].type)
                    {
                        case Task.TaskType.Action:
                            if (state == JobStates.Failed)
                            {
                                Failed(curTaskIdx);
                                yield break;
                            }
                            else if (state == JobStates.Stopped)
                            {
                                Stopped(curTaskIdx);
                                yield break;
                            }

                            if (tasks[curTaskIdx].action != null)
                            try
                            {
                                tasks[curTaskIdx].action();
                            }
                            catch (Exception ex)
                            {
#if UNITY_EDITOR
                                Debug.LogError(ex);
#else
                                Debug.LogWarning("Error Job.Run: " + ex);
#endif

                                Failed(curTaskIdx);
                                yield break;
                            }
                            yield return null;
                            break;
                        case Task.TaskType.Condition:
                            while (!tasks[curTaskIdx].condition()) //TODO: no endless loop possible?
                            {
                                if (state == JobStates.Failed)
                                {
                                    Failed(curTaskIdx);
                                    yield break;
                                }
                                else if (state == JobStates.Stopped)
                                {
                                    Stopped(curTaskIdx);
                                    yield break;
                                }
                                yield return null;
                            }
                            break;
                        case Task.TaskType.Timer:
                            yield return new WaitForSeconds(tasks[curTaskIdx].time);
                            break;
                        default:
                            break;
                    }
                }

#if DEBUG
                TimeSpan result = DateTime.Now.Subtract(startTaskTime);
                float duration = (float)result.TotalMilliseconds;
                int frDuration = Time.frameCount - startTaskFrame;
                Debug.LogWarning("Job: " + name + " Task " + curTaskIdx + " finished! Time: " + duration.ToString("f2") + "ms, " + frDuration + " fr");
#endif
            }

            Finished();
        }

        void Finished()
        {
            //float duration = Time.time - startTime;
            TimeSpan result = DateTime.Now.Subtract(startTime);
            float duration = (float)result.TotalMilliseconds;
            int frDuration = Time.frameCount - startFrame;
            Debug.Log("Job: " + name + " with " + tasks.Length + " tasks finished! Time: " + duration.ToString("f2") + "ms, " + frDuration + " fr");
            tasks = null;
            state = JobStates.Done;

            if (OnFinished != null)
                OnFinished();
        }

        void Failed(int taskIdx)
        {
            TimeSpan result = DateTime.Now.Subtract(startTime);
            float duration = (float)result.TotalMilliseconds;
            int frDuration = Time.frameCount - startFrame;
            Debug.LogWarning("Job: " + name + " failed at task " + taskIdx + "! Time: " + duration.ToString("f2") + "ms, " + frDuration + " fr");
            tasks = null;
            state = JobStates.Done;

            if (OnFailed != null)
                OnFailed();
        }

        void Stopped(int taskIdx)
        {
            TimeSpan result = DateTime.Now.Subtract(startTime);
            float duration = (float)result.TotalMilliseconds;
            int frDuration = Time.frameCount - startFrame;
            Debug.Log("Job: " + name + " Stopped at task " + taskIdx + ". Time: " + duration.ToString("f2") + "ms, " + frDuration + " fr");
            tasks = null;
            state = JobStates.Done;

            if (OnStopped != null)
                OnStopped();
        }

        public string PrintDebug()
        {
            return "Debug Jobs: " + name + ", state: " + state.ToString() + ", current Task: " + curTaskIdx;
        }
    }

    public class Task
    {
        public enum TaskType
        {
            Action,
            Condition,
            Timer
        }

        public TaskType type;
        public Action action;
        public Func<bool> condition;
        public float time;

        public Task(Action action)
        {
            this.type = TaskType.Action;
            this.action = action;
            this.condition = null;
            this.time = 0f;
        }

        public Task(Func<bool> condition)
        {
            this.type = TaskType.Condition;
            this.action = null;
            this.condition = condition;
            this.time = 0f;
        }

        public Task(float time)
        {
            this.type = TaskType.Timer;
            this.action = null;
            this.condition = null;
            this.time = time;
        }
    }
}
