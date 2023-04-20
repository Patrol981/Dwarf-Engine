using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Dwarf.Engine;
using Dwarf.Engine.EntityComponentSystem;
using Dwarf.Engine.Loaders;
using Dwarf.Extensions.Logging;
using Dwarf.Vulkan;

using OpenTK.Mathematics;

namespace DwarfEngine.AssetSystem;
public class AssetLoader {
  public static Entity LoadAsset(Application app, string path) {
    var entity = new Entity();

    var file = File.OpenRead(path);
    var contents = SeekFile(ref file, ref entity, app.Device);

    var allComponents = entity.GetComponentManager().GetAllComponents();
    foreach (var component in allComponents) {
      ProcessComponent(app, component.Value.GetType(), contents, entity);
    }

    file.Dispose();
    return entity;
  }

  private static string SeekFile(ref FileStream file, ref Entity entity, Device device) {
    using var reader = new StreamReader(file, Encoding.UTF8, true);
    string fullData = string.Empty;
    string line = string.Empty;
    while ((line = reader.ReadLine()!) != null) {
      fullData += line;
      ProcessLine(line, ref entity, device);
    }
    reader.Dispose();
    return fullData;
  }

  private static void ProcessLine(string line, ref Entity entity, Device device) {
    if (line != string.Empty) {
      if (line.Contains($"[{AssetConstants.AssetNameKeyword}]")) {
        ProcessAssetName(line, ref entity);
      }

      if (line.Contains($"[{AssetConstants.AssetEngineDependeciesKeyword}]")) {
        ProcessDependencies(line, ref entity, device);
      }
    }
  }

  private static void ProcessAssetName(string line, ref Entity entity) {
    string pattern = @"=(.*)$";
    var match = Regex.Match(line, pattern);
    if (!match.Success) throw new Exception("Asset File Corrupted");

    entity.Name = match.Groups[1].Value.Trim();
  }

  private static void ProcessDependencies(string line, ref Entity entity, Device device) {
    string keyword = $"[{AssetConstants.AssetEngineDependeciesKeyword}]";
    int keywordLen = keyword.Length;
    string pattern = @"\[(.*?)\]";
    string dwarfEnginePrefix = "Dwarf.Engine";

    var newLine = line.Substring(keywordLen);
    var match = Regex.Match(newLine, pattern);
    if (!match.Success) throw new Exception("Asset File Corrupted");

    string words = match.Groups[1].Value;
    string[] wordsArray = words.Split(",").Select(w => w.Trim()).ToArray();
    foreach (var word in wordsArray) {
      var targetString = word.Contains(".") ? word : $"{dwarfEnginePrefix}.{word}";
      Logger.Info(targetString);
      Type componentType = Type.GetType(targetString)!;
      if (componentType == null || !componentType.IsSubclassOf(typeof(Component)))
        throw new Exception("Unknown Entity Component Type.");
      object[] constructorArgs = HandleConstructorData(device, componentType);
      Component component = (Component)Activator.CreateInstance(componentType, constructorArgs)!;
      MethodInfo addComponentMethod = entity.GetType().GetMethod("AddComponent", BindingFlags.Instance | BindingFlags.Public)!;
      addComponentMethod.Invoke(entity, new object[] { component });
    }
  }

  private static void ProcessComponent(Application app, Type componentType, string file, Entity entity) {
    string pattern = @"\[" + componentType.Name + @"\] = {(.*?)\}";

    var match = Regex.Match(file, pattern, RegexOptions.Singleline | RegexOptions.Multiline);
    if (!match.Success) Logger.Warn("Pattern not found");

    HandleComponentData(app, componentType, entity, match.Value);

    Logger.Info(match.ToString());
  }

  private static object[] GetTransformData(string data) {
    string pattern = @"-?\d+(\.\d+)?,(-?\d+(\.\d+)?),(-?\d+(\.\d+)?)";
    var valuesList = new List<float>();
    var matches = Regex.Matches(data, pattern, RegexOptions.Singleline | RegexOptions.Multiline);

    foreach (Match match in matches) {
      if (!match.Success) {
        Logger.Warn("Transform Data not found");
        continue;
      }

      string[] stringValues = match.ToString().Split(',');
      foreach (string value in stringValues) {
        var number = float.Parse(value, CultureInfo.InvariantCulture.NumberFormat);
        valuesList.Add(number);
      }
    }

    return new object[] {
      new Vector3(valuesList[0], valuesList[1], valuesList[2]),
      new Vector3(valuesList[3], valuesList[4], valuesList[5]),
      new Vector3(valuesList[6], valuesList[7], valuesList[8])
    };
  }

  private static object[] GetMaterialData(string data) {
    string pattern = @"-?\d+(\.\d+)?,(-?\d+(\.\d+)?),(-?\d+(\.\d+)?)";
    var valuesList = new List<float>();
    var matches = Regex.Matches(data, pattern, RegexOptions.Singleline | RegexOptions.Multiline);

    foreach (Match match in matches) {
      if (!match.Success) {
        Logger.Warn("Transform Data not found");
        continue;
      }

      string[] stringValues = match.ToString().Split(',');
      foreach (string value in stringValues) {
        var number = float.Parse(value, CultureInfo.InvariantCulture.NumberFormat);
        valuesList.Add(number);
      }
    }

    return new object[] {
      new Vector3(valuesList[0], valuesList[1], valuesList[2])
    };
  }

  private static object[] GetSpriteData(string data) {
    var localTexturePattern = @"\[LocalTexture\] = (.*?)\s";
    var texturePathPattern = @"\[TexturePath\] = (.*?)\}";

    var localTextureMatch = Regex.Match(data, localTexturePattern, RegexOptions.Singleline | RegexOptions.Multiline);
    if (!localTextureMatch.Success) Logger.Warn("Local Texture Pattern not found");

    var texturePathMatch = Regex.Match(data, texturePathPattern, RegexOptions.Singleline | RegexOptions.Multiline);
    if (!texturePathMatch.Success) Logger.Warn("Texture Path Pattern not found");

    bool.TryParse(localTextureMatch.Groups[1].Value, out var localTexture);
    var texturePath = texturePathMatch.Groups[1].Value;

    return new object[] { localTexture, texturePath };
  }

  private static object[] GetModelData(string data) {
    var localTexturePattern = @"\[LocalTexture\] = (.*?)\s";
    var texturePathPattern = @"\[TexturePath\] = (.*?)\[";
    var modelPathPattern = @"\[ModelPath\] = (.*?)\[";
    var usesLightPattern = @"\[UsesLight\] = (.*?)\}";

    var localTextureMatch = Regex.Match(data, localTexturePattern, RegexOptions.Singleline | RegexOptions.Multiline);
    if (!localTextureMatch.Success) Logger.Warn("Local Texture Pattern not found");

    var texturePathMatch = Regex.Match(data, texturePathPattern, RegexOptions.Singleline | RegexOptions.Multiline);
    if (!texturePathMatch.Success) Logger.Warn("Texture Path Pattern not found");

    var modelPathMatch = Regex.Match(data, modelPathPattern, RegexOptions.Singleline | RegexOptions.Multiline);
    if (!modelPathMatch.Success) Logger.Warn("Model Path Pattern not found");

    var usesLightMatch = Regex.Match(data, usesLightPattern, RegexOptions.Singleline | RegexOptions.Multiline);
    if (!usesLightMatch.Success) Logger.Warn("Use Light Pattern not found");

    bool.TryParse(localTextureMatch.Groups[1].Value, out var localTexture);
    bool.TryParse(usesLightMatch.Groups[1].Value, out var usesLight);

    var texturePath = texturePathMatch.Groups[1].Value.TrimEnd();
    var modelPath = modelPathMatch.Groups[1].Value.TrimEnd();

    return new object[] { localTexture, texturePath, modelPath, usesLight };
  }

  private static void HandleComponentData(Application app, Type componentType, Entity entity, string componentData) {
    object component = Activator.CreateInstance(componentType)!;

    switch (component) {
      case Transform:
        var transformData = GetTransformData(componentData);
        entity.GetComponent<Transform>().Position = (Vector3)transformData[0];
        entity.GetComponent<Transform>().Scale = (Vector3)transformData[1];
        entity.GetComponent<Transform>().Rotation = (Vector3)transformData[2];
        break;
      case Material:
        var materialData = GetMaterialData(componentData);
        entity.GetComponent<Material>().SetColor((Vector3)materialData[0]);
        break;
      case Sprite:
        var spriteData = GetSpriteData(componentData);
        entity.GetComponent<Sprite>().BindToTexture(app.TextureManager, (string)spriteData[1], (bool)spriteData[0]);
        break;
      case Model:
        var modelData = GetModelData(componentData);
        entity.AddComponent(new GenericLoader().LoadModel(app.Device, (string)modelData[2]));
        entity.GetComponent<Model>().BindToTexture(app.TextureManager, (string)modelData[1], (bool)modelData[0]);
        entity.GetComponent<Model>().UsesLight = (bool)modelData[3];
        break;
      default:
        break;
    }
  }

  private static object[] HandleConstructorData(Device device, Type componentType) {
    object component = Activator.CreateInstance(componentType)!;

    switch (component) {
      case Transform:
        return new object[] { new Vector3(0, 0, 0) };
      case Material:
        return new object[] { new Vector3(1, 1, 1) };
      case Sprite:
        return new object[] { device };
      default:
        return Array.Empty<object>();
    }
  }
}
