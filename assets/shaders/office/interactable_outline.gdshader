shader_type canvas_item;

// Set per sprite from code by InteractableFeedback: 0 at rest, 1 while hovered.
instance uniform float hover = 0.0;

uniform vec4 outline_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

void fragment() {
	vec2 px = TEXTURE_PIXEL_SIZE;
	vec4 art = texture(TEXTURE, UV);

	// Largest alpha among the four neighbours, each exactly one texel away.
	float neighbour_alpha = max(
		max(texture(TEXTURE, UV + vec2(px.x, 0.0)).a, texture(TEXTURE, UV - vec2(px.x, 0.0)).a),
		max(texture(TEXTURE, UV + vec2(0.0, px.y)).a, texture(TEXTURE, UV - vec2(0.0, px.y)).a)
	);

	// Mostly-transparent texels touching the silhouette: a ring one texel wide, drawn outside the art.
	// Scaled by hover in colour and alpha, so at rest the output equals the untouched sprite.
	float ring = step(0.5, neighbour_alpha) * (1.0 - step(0.5, art.a)) * hover;

	COLOR = vec4(mix(COLOR.rgb, outline_color.rgb, ring), max(COLOR.a, ring));
}
