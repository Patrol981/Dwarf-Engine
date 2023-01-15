mkdir CompiledShaders

for /r %%i in (*.frag, *.vert) do C:\VulkanSDK\1.3.236.0\Bin\glslc.exe %%i -o ./CompiledShaders/%%~ni.spv
