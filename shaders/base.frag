#version 450

layout(location = 0) in vec4 fragColor;
layout(location = 1) in vec2 uv;

layout(location = 0) out vec4 outColor;


void main() {
    float r = length(uv);
    if (r >=1)
    {
        outColor = vec4(0);
        return;
    }


    outColor = vec4(pow(fragColor.xyz, vec3(1/2.2)), 1);
}