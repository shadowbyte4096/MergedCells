shader_type canvas_item;

// The startup intro's scene, painted from distances so every edge is antialiased once, and the player and its trail
// are one shape (two overlapping shapes would double their soft edges where they meet). Lengths are in viewport units
// unless the comment says icon units; IntroTimeline (Core) works out each frame and IntroLayer passes it in.

uniform vec4 background : source_color;
uniform vec4 ink : source_color;
uniform vec4 left_fill : source_color;
uniform vec4 right_fill : source_color;
uniform float background_alpha = 1.0;
uniform float scene_alpha = 1.0;
uniform vec2 circle_centre;
uniform float circle_radius = 1.0;
uniform vec2 art_origin;
uniform float art_scale = 1.0;
// Icon units: x, y, width, height, with the stretched ends applied
uniform vec4 left_block;
uniform vec4 right_block;
// Icon units: corner, outline (centred on the edge, as in SVG)
uniform vec2 left_shape;
uniform vec2 right_shape;
// Icon units: x, y, side, outline (centred on the edge)
uniform vec4 player;
uniform float player_corner;
uniform float trail_start;
uniform float fade_end;

varying vec2 local;

void vertex() {
	local = VERTEX;
}

// Signed distance to a rounded box, negative inside
float box(vec2 p, vec2 centre, vec2 half_size, float radius) {
	vec2 q = abs(p - centre) - half_size + radius;
	return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}

// How much of this pixel a distance covers, antialiased over one pixel
float cover(float d) {
	return clamp(0.5 - d / max(fwidth(d), 1e-5), 0.0, 1.0);
}

// Premultiplied "over"
vec4 over(vec4 under, vec3 colour, float alpha) {
	return vec4(colour * alpha, alpha) + under * (1.0 - alpha);
}

vec4 block(vec4 under, vec2 p, vec4 rect, vec2 shape, vec3 fill, float alpha) {
	float d = box(p, rect.xy + rect.zw / 2.0, rect.zw / 2.0, shape.x);
	vec4 c = over(under, fill, cover(d) * alpha);
	return over(c, ink.rgb, cover(abs(d) - shape.y / 2.0) * alpha);
}

void fragment() {
	vec2 p = (local - art_origin) / art_scale;
	vec4 c = vec4(background.rgb * background_alpha, background_alpha);
	float alpha = scene_alpha * cover(length(local - circle_centre) - circle_radius);

	c = block(c, p, left_block, left_shape, left_fill.rgb, alpha);
	c = block(c, p, right_block, right_shape, right_fill.rgb, alpha);

	float half_outline = player.w / 2.0;
	vec2 centre = player.xy + player.z / 2.0;
	float outer_half = player.z / 2.0 + half_outline;
	float outer = box(p, centre, vec2(outer_half), player_corner + half_outline);
	float trail = box(p, vec2((trail_start + centre.x) / 2.0, centre.y), vec2((centre.x - trail_start) / 2.0, outer_half), 0.0);
	float hole = box(p, centre, vec2(player.z / 2.0 - half_outline), max(player_corner - half_outline, 0.0));
	float fade = clamp((p.x - trail_start) / max(fade_end - trail_start, 1e-5), 0.0, 1.0);
	c = over(c, ink.rgb, cover(max(min(outer, trail), -hole)) * fade * alpha);

	COLOR = c.a > 0.0 ? vec4(c.rgb / c.a, c.a) : vec4(0.0);
}
