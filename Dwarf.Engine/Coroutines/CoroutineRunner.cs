using System.Collections;

using Dwarf.Extensions.Logging;

namespace Dwarf.Engine.Coroutines;

public sealed class CoroutineRunner {
  private readonly Dictionary<Type, CoroutineItem> _tasks = [];

  public async void StartCoroutine(IEnumerator coroutine) {
    if (!_tasks.ContainsKey(coroutine.GetType())) {
      var item = new CoroutineItem();
      item.CoroutineTask = StartCoroutineAsync(coroutine, item.TokenSource.Token);
      _tasks.Add(coroutine.GetType(), item);
      await Task.Run(() => Task.WaitAll(_tasks[coroutine.GetType()].CoroutineTask), item.TokenSource.Token);
      _tasks.Remove(coroutine.GetType());
    } else {
      Logger.Warn("Coroutine is already running; Ignoring current request.");
    }
  }

  public async Task<Task> StopCoroutine(IEnumerator coroutine) {
    var tasks = new List<Task>();
    var itemsToRemove = new List<Type>();

    foreach (var item in _tasks) {
      if (item.Key == coroutine.GetType()) {
        tasks.Add(Task.Run(() => StopWork(item.Value)));
        itemsToRemove.Add(item.Key);
        break;
      }
    }

    await Task.WhenAll(tasks);
    foreach (var item in itemsToRemove) {
      _tasks.Remove(item);
    }
    return Task.CompletedTask;
  }

  private static void StopWork(CoroutineItem coroutineItem) {
    coroutineItem.TokenSource.Cancel();
    coroutineItem.CoroutineTask.Wait();
  }

  public Task StopAllCoroutines() {
    var tasks = new List<Task>();

    foreach (var item in _tasks) {
      tasks.Add(Task.Run(() => StopWork(item.Value)));
    }

    return Task.WhenAll(tasks);
  }

  public async Task StartCoroutineAsync(IEnumerator coroutine, CancellationToken cancellationToken) {
    try {
      cancellationToken.ThrowIfCancellationRequested();
      await ExecuteAsync(coroutine, cancellationToken);
    } catch (OperationCanceledException) {
      Logger.Info($"Task cancelled");
    }
  }

  private async Task ExecuteAsync(IEnumerator coroutine, CancellationToken cancellationToken) {
    try {
      cancellationToken.ThrowIfCancellationRequested();
      while (coroutine.MoveNext()) {
        var current = coroutine.Current;
        cancellationToken.ThrowIfCancellationRequested();
        if (current is WaitForSeconds) {
          var waitForSeconds = (WaitForSeconds)current;
          await Task.Delay(TimeSpan.FromSeconds(waitForSeconds.Seconds));
        } else if (current == null || current is YieldInstruction) {
          await Task.Yield();
        }
      }
    } catch (OperationCanceledException) {
      Logger.Info($"Coroutine Exec cancelled");
    }
  }

  public static CoroutineRunner Instance { get; } = new();
}
