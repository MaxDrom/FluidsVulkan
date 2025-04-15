#version 450

layout(set = 0, binding = 0) uniform sampler2D fontTexture;

layout(location = 0) in vec2 fragUV;
layout(location = 1) in vec4 fragColor;

layout(location = 0) out vec4 outColor;

void main() {
    vec4 texColor = texture(fontTexture, fragUV);
    outColor = fragColor * texColor;
}