if not exist CompiledShaders mkdir CompiledShaders

for /r %%i in (*.frag, *.vert) do glslc %%i -o ./CompiledShaders/%%~ni.spv
