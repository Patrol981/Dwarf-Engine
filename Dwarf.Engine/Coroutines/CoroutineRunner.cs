using System.Collections;

using Dwarf.Extensions.Logging;

namespace Dwarf.Engine.Coroutines;
public sealed class CoroutineRunner {
  private IEnumerator _coroutine = null!;

  private readonly Dictionary<IEnumerator, CoroutineItem> _tasks = [];
  private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();

  public async void StartCoroutine(IEnumerator coroutine) {
    if (!_tasks.ContainsKey(coroutine)) {
      var item = new CoroutineItem();
      item.CoroutineTask = StartCoroutineAsync(coroutine, item.TokenSource.Token);
      _tasks.Add(coroutine, item);
      await Task.Run(() => Task.WaitAll(_tasks[coroutine].CoroutineTask), _tokenSource.Token);
    } else {
      throw new InvalidOperationException("Coroutine is already running.");
    }
  }

  public void StopCoroutine(IEnumerator coroutine) {
    foreach (var item in _tasks) {
      if (item.Key.GetType() == coroutine.GetType()) {
        item.Value.TokenSource.Cancel();
      }
    }
  }

  public void StopAllCoroutines() {
    foreach (var item in _tasks) {
      item.Value.TokenSource.Cancel();
    }
  }

  public async Task StartCoroutineAsync(IEnumerator coroutine, CancellationToken cancellationToken) {
    try {
      _coroutine = coroutine;
      await ExecuteAsync(cancellationToken);

      cancellationToken.ThrowIfCancellationRequested();
    } catch (OperationCanceledException) {
      Logger.Info($"Task cancelled");
    }

  }

  private async Task ExecuteAsync(CancellationToken cancellationToken) {
    try {
      while (_coroutine.MoveNext()) {
        var current = _coroutine.Current;
        if (current is WaitForSeconds) {
          var waitForSeconds = (WaitForSeconds)current;
          await Task.Delay(TimeSpan.FromSeconds(waitForSeconds.Seconds));
        } else if (current == null || current is YieldInstruction) {
          await Task.Yield();
        }
        cancellationToken.ThrowIfCancellationRequested();
      }
    } catch (OperationCanceledException) {
      Logger.Info($"Coroutine Exec cancelled");
    }
  }

  public static CoroutineRunner Instance { get; } = new();
}
