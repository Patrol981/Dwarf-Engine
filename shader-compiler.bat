if not exist CompiledShaders mkdir CompiledShaders
if not exist TranspiledShaders mkdir TranspiledShaders

# for /r %%i in (*.frag, *.vert) do glslc %%i -o ./CompiledShaders/%%~ni.spv

cd ./Dwarf.ShaderLanguage/
call cargo run ../Shaders ../TranspiledShaders
cd ..
for %%i in (TranspiledShaders\*.frag TranspiledShaders\*.vert) do glslc %%i -o CompiledShaders\%%~ni.spv