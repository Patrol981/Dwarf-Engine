if not exist CompiledShaders mkdir CompiledShaders
if not exist TranspiledShaders mkdir TranspiledShaders

cd ./Dwarf.ShaderLanguage/
call cargo run ../Shaders ../TranspiledShaders
cd ..
for %%i in (TranspiledShaders\*.frag TranspiledShaders\*.vert) do glslang --target-env vulkan1.4 --glsl-version 460 %%i -o CompiledShaders\%%~ni.spv