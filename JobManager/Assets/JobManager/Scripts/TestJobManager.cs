using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JobManagerSpace;

public class TestJobManager : MonoBehaviour 
{
    public bool testCondition = false; //switch in Inspector

    [ContextMenu("Test Jobs")]
    private void TestJobs()
    {
        List<Task> tasks = new List<Task>();
        tasks.Add(Job.NewTask(
            () =>
            {
                Debug.LogWarning("task1");
            }
            ));
        tasks.Add(Job.NewTask(
            () =>
            {
                // if testCondition is true -> do next Task
                return testCondition;
            }
            ));
        tasks.Add(Job.NewTask(
            () =>
            {
                Debug.LogWarning("task3");
            }
            ));

        tasks.Add(Job.NewTask(0.123f)); // waits 0.123 seconds

        tasks.Add(Job.NewTask(
            () =>
            {
                testCondition = false;
                Debug.LogWarning("finished!");
            }
            ));

        Job job = JobManager.NewJob("TestJob", tasks.ToArray());
        JobManager.I.AddJob(job);
    }
}
