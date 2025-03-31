using Dwarf.EntityComponentSystem;
using Dwarf.Model.Animation;
using Dwarf.Utils;
using Dwarf.WebApi;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Dwarf.Networking;
public class WebApiSystem : IDisposable {
  private readonly Application? _application;
  private readonly WebInstance? _webInstance;

  public class RotationData {
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
  }

  public class TranslationData {
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
  }

  public WebApiSystem(Application app) {
    _application = app;
    _webInstance = new WebInstance();
    _webInstance.OnMap += MapEndpoints;
    _webInstance.Init();
  }

  public void MapEndpoints() {
    _webInstance?.WebApplication?.MapGet("/ping", () => {
      return Results.Ok("pong");
    });

    _webInstance?.WebApplication?.MapPost("/model", async (IFormFile file) => {
      if (file == null || file.Length == 0 || Path.GetExtension(file.FileName).ToLower() != ".glb") {
        return Results.BadRequest("Please upload a valid .glb file.");
      }

      var filePath = Path.Combine(DwarfPath.AssemblyDirectory, "Resources", file.FileName);
      using (var stream = new FileStream(filePath, FileMode.Create)) {
        await file.CopyToAsync(stream);
      }

      _application?.Mutex.WaitOne();
      var postModel = new Entity();
      postModel.Name = postModel.EntityID.ToString();
      postModel.AddTransform();
      postModel.AddMaterial();
      // postModel.AddRigidbody(PrimitiveType.Box, new(.5f, 1f, .5f), new(0, -1f, 0), true, false);
      postModel.AddModel(filePath, 0);
      postModel.GetComponent<AnimationController>().PlayFirstAnimation();

      _application?.AddEntity(postModel);
      _application?.Mutex.ReleaseMutex();

      return Results.Ok();
    }).DisableAntiforgery();

    _webInstance?.WebApplication?.MapPost("/rotate", (string id, RotationData rotation) => {
      var target = _application!.GetEntity(Guid.Parse(id));

      if (target == null) {
        return Results.NotFound();
      }

      target.GetComponent<Transform>().Rotation.X = rotation.X;
      target.GetComponent<Transform>().Position.Y = rotation.Y;
      target.GetComponent<Transform>().Position.Z = rotation.Z;

      return Results.Ok();
    });

    _webInstance?.WebApplication?.MapPost("/translate", (string id, TranslationData transform) => {
      var target = _application!.GetEntity(Guid.Parse(id));

      if (target == null) {
        return Results.NotFound();
      }

      target.GetComponent<Transform>().Position.X = transform.X;
      target.GetComponent<Transform>().Position.Y = transform.Y;
      target.GetComponent<Transform>().Position.Z = transform.Z;

      return Results.Ok();
    });
  }

  public void Dispose() {
    _webInstance?.Dispose();
  }
}
