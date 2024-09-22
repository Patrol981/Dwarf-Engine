using System.Text;
using System.Text.Json;

namespace Dwarf.Loaders;

public static class DwarfFileLoader {
  public static MeshRenderer LoadMesh(Application app, string path) {
    var dwarfFile = Load(path);
    var meshRenderer = new MeshRenderer(app.Device, app.Renderer);

    // Load Textures From binary file
    var stream = new FileStream($"./{dwarfFile.BinaryDataRef}", FileMode.Open);
    var reader = new BinaryReader(stream);

    if (dwarfFile.Nodes?.Count == 0) {
      throw new ArgumentException(nameof(dwarfFile.Nodes));
    }
    foreach (var node in dwarfFile.Nodes!) {
      LoadNode(null!, node, ref meshRenderer, reader, app, in dwarfFile);
    }

    return meshRenderer;
  }

  public static DwarfFile Load(string path) {
    try {
      var file = LoadFromFile(path);

      return JsonSerializer.Deserialize<DwarfFile>(file, DwarfFileParser.ParserOptions)
      ?? throw new Exception("File is null");
    } catch {
      throw;
    }
  }

  private static string LoadFromFile(string path) {
    return File.ReadAllText(path);
  }

  private static void LoadNode(
    Dwarf.Model.Node parentNode,
    FileNode fileNode,
    ref MeshRenderer meshRenderer,
    BinaryReader reader,
    Application app,
    in DwarfFile dwarfFiile
  ) {
    var newNode = FileNode.FromFileNode(fileNode, parentNode);

    if (fileNode.Mesh != null) {
      var offset = fileNode.Mesh.BinaryOffset;
      var refId = fileNode.Mesh.BinaryReferenceName;

      reader.BaseStream.Seek((long)offset, SeekOrigin.Begin);

      var guidBytes = reader.ReadBytes(36);
      Guid guid = Guid.Parse(Encoding.UTF8.GetString(guidBytes));

      if (guid.ToString() != fileNode.Mesh.BinaryReferenceName) {
        throw new ArgumentException("Mismatch between guid of texture.");
      }

      byte[] textureData = reader.ReadBytes((int)fileNode.Mesh.BinaryTextureSize);

      var texture = VulkanTexture.LoadFromBytes(
        app.Device,
        textureData,
        fileNode.Mesh.TextureFileName,
        dwarfFiile.TextureFlipped
      );
      var id = app.TextureManager.AddTexture(texture);

      // newNode.Mesh!.TextureIdReference = id;
      newNode.Mesh!.BindToTexture(app.TextureManager, id);
    }

    if (parentNode == null) {
      meshRenderer.AddNode(newNode);
    }
    meshRenderer.AddLinearNode(newNode);
  }
}