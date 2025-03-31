#!/bin/bash

# Create directories if they do not exist
mkdir -p CompiledShaders
mkdir -p TranspiledShaders

# Navigate to the Dwarf.ShaderLanguage directory
cd ./Dwarf.ShaderLanguage/ || exit

# Run cargo with the given arguments
cargo run ../Shaders ../TranspiledShaders

# Return to the original directory
cd ..

# Compile GLSL shaders to SPIR-V
for i in TranspiledShaders/*.frag TranspiledShaders/*.vert; do
    base_name=$(basename "$i")  # Extract the file name
    output_name="CompiledShaders/${base_name%.*}.spv"  # Change extension to .spv

    # Compile shader
    glslang --target-env vulkan1.4 --glsl-version 460 "$i" -o "$output_name"
done
