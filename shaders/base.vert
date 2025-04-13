#version 450
layout(location = 0) in vec2 inPosition;
layout(location = 1) in vec4 inColor;
layout(location = 2) in vec2 instancePos;
layout(location = 3) in vec4 instanceCol;
layout(location = 4) in vec2 instanceOffset;

layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec2 uv;

layout(push_constant) uniform PushConstants {
    vec2 xrange;
    vec2 yrange;
}push;
void main() {
    vec2 dpos = vec2((push.xrange.y - push.xrange.x), (push.yrange.y - push.yrange.x));
    
    vec2 pos = vec2(instancePos.x -push.xrange.x, instancePos.y - push.yrange.x)/dpos*2.0+vec2(-1, -1);
    
    
    vec2 newPos = inPosition*0.001f/dpos + pos;
    
    gl_Position = vec4(newPos, 0.0, 1.0);
    fragColor = instanceCol;
    uv = inPosition;
}