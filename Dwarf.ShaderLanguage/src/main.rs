pub mod shader_objects;

use std::{
  env,
  fs::{self, File},
  io::Write,
  path::PathBuf,
};

use shader_objects::shader_objects::{ShaderCode, ShaderStructObject};
fn main() {
  // call structure
  // dsl.exe SRC_DIR DST_DIR

  let args: Vec<String> = env::args().collect();

  let src_dir = &args[1];
  let dst_dir = &args[2];

  let cur_dir = match env::current_dir() {
    Ok(path) => path,
    Err(err) => {
      println!("Could not get current directory data. [{err}]");
      return;
    }
  };

  println!("current dir -> {}", cur_dir.display());
  println!("src dir -> {src_dir}");
  println!("dst dir -> {dst_dir}");

  // list all structures in src directory
  let combined_path = cur_dir.join(src_dir).join("structs");
  let struct_paths = match fs::read_dir(combined_path) {
    Ok(result) => result,
    Err(err) => {
      println!("Could not get src directory path. [{err}]");
      return;
    }
  };

  let mut shader_structs: Vec<ShaderStructObject> = Vec::new();
  for path in struct_paths {
    let path_buf = path.unwrap().path();
    if !path_buf.is_file() {
      continue;
    }
    shader_structs.push(ShaderStructObject::new(
      get_file_name(&path_buf),
      read_file(&path_buf),
    ))
  }

  // get all vertex and fragment files
  let combined_path = cur_dir.join(src_dir);
  let shader_paths = match fs::read_dir(combined_path) {
    Ok(result) => result,
    Err(err) => {
      println!("Could not get src directory path. [{err}]");
      return;
    }
  };

  let mut shader_codes: Vec<ShaderCode> = Vec::new();
  for path in shader_paths {
    let path_buf = path.unwrap().path();
    if !path_buf.is_file() {
      continue;
    }
    shader_codes.push(ShaderCode::new(
      get_file_name(&path_buf),
      get_file_name_ext(&path_buf),
      read_file(&path_buf),
    ))
  }

  for mut sc in shader_codes {
    edit_shader_code(&mut sc, &shader_structs);
    let dst_path = cur_dir.join(dst_dir).join(sc.file_name_ext.clone());
    write_file(&dst_path, &sc.data);
  }
}

fn read_file(path: &PathBuf) -> String {
  let file_data = match fs::read_to_string(path) {
    Ok(result) => result,
    Err(err) => {
      panic!("[{}] {err}", path.display());
    }
  };

  return file_data;
}

fn get_file_name(path: &PathBuf) -> String {
  let file_name = path.file_stem().unwrap().to_str().unwrap();
  return file_name.to_string();
}

fn get_file_name_ext(path: &PathBuf) -> String {
  let file_name = path.file_name().unwrap().to_str().unwrap();
  return file_name.to_string();
}

fn edit_shader_code(shader_code: &mut ShaderCode, shader_structs: &Vec<ShaderStructObject>) {
  let mut modified_data = shader_code.data.clone();

  for shader_struct in shader_structs {
    let include_directive = format!("#include {}", shader_struct.token);
    if modified_data.contains(&include_directive) {
      modified_data = modified_data.replace(&include_directive, &shader_struct.data)
    }
  }

  shader_code.data = modified_data;
}

fn write_file(path: &PathBuf, data: &str) {
  let mut file = match File::create(path) {
    Ok(result) => result,
    Err(err) => {
      panic!("{err}");
    }
  };

  match file.write_all(data.as_bytes()) {
    Ok(result) => result,
    Err(err) => {
      panic!("{err}");
    }
  };
}
