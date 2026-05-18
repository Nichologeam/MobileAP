using System;
using System.Collections.Concurrent;
using UnityEngine;

public class ThreadManager : MonoBehaviour
{
    private static readonly ConcurrentQueue<Action> queue = new();

    public static void RunOnMainThread(Action action)
    {
        queue.Enqueue(action);
    }

    private void Update()
    {
        while (queue.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }
}