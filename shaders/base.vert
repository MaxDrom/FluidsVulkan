#version 450
layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec4 inColor;
layout(location = 2) in vec2 instancePos;
layout(location = 3) in vec2 instanceOffset;
layout(location = 4) in float density;
layout(location = 5) in float pressure;

layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec2 uv;

layout(push_constant) uniform PushConstants {
    vec2 xrange;
    vec2 yrange;
    vec2 minMax;
    int visualizationIndex;
}push;


vec3 temperatureGradient(float t) {
    t = clamp(t, 0.0, 1.0);

    if (t < 0.25) {
        float nt = t / 0.25;
        return vec3(0.0, nt, 1.0);// blue → cyan
    } else if (t < 0.5) {
        float nt = (t - 0.25) / 0.25;
        return vec3(0.0, 1.0, 1.0 - nt);// cyan → green
    } else if (t < 0.75) {
        float nt = (t - 0.5) / 0.25;
        return vec3(nt, 1.0, 0.0);// green → yellow
    } else {
        float nt = (t - 0.75) / 0.25;
        return vec3(1.0, 1.0 - nt, 0.0);// yellow → red
    }
}

float remap(float val)
{
    return max(0, (val -push.minMax.x)/max(dot(push.minMax, vec2(-1, 1)), 0.00001));
}

float array[3] = float[3] (remap(density), remap(length(instanceOffset)), remap(pressure));
void main() {
    vec2 dpos = vec2((push.xrange.y - push.xrange.x), (push.yrange.y - push.yrange.x));

    vec2 pos = vec2(instancePos.x -push.xrange.x, instancePos.y - push.yrange.x)/dpos*2.0+vec2(-1, -1);


    vec2 newPos = inPosition*0.001f/dpos + pos;

    gl_Position = vec4(newPos, 0.0, 1.0);
    fragColor = vec4(temperatureGradient(array[push.visualizationIndex]), 1);
    uv = inPosition;
}