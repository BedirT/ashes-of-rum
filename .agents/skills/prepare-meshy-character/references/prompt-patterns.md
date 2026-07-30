# Prompt Patterns

Adapt these patterns to the approved image rather than copying placeholders literally. Repeat distinctive visual facts in every prompt because each Firefly request is independent.

## Shared Identity Block

Summarize the approved character in one compact block:

> The exact same [ROLE] shown in the supplied approved concept: [BODY AND PROPORTIONS], [FACE TREATMENT], [CLOTHING AND ARMOR], [PALETTE AND MATERIALS]. Preserve the exact character design, proportions, silhouette, colors, construction, and simplified detail level. Do not redesign or add details.

Mention each asymmetric body-worn item and its placement from the character's perspective.

## Front A-pose

> Create a clean full-body front view of [IDENTITY BLOCK]. Relaxed symmetrical A-pose, arms angled slightly away from the torso, legs straight and slightly apart, hands relaxed and empty. Retain [WORN ITEMS AND LOCATIONS]. Remove [HELD/SEPARATE ITEMS]. Character looking directly forward. Entire figure centered and visible from head to feet, orthographic-like camera, no perspective distortion. Pure white background, flat neutral lighting, no cast shadow or floor shadow. One character only; no text, scenery, base, turntable, or extra objects.

## Back A-pose

> Using the supplied approved concept/front image as strict identity reference, create the exact same character seen directly from behind. [IDENTITY BLOCK]. Preserve the rear construction of all clothing, armor, straps, and [WORN ITEMS AND LOCATIONS]; infer only surfaces hidden in the source and keep them simple and functional. Same relaxed symmetrical A-pose and scale as the front image, empty hands, full body centered, orthographic-like rear view. Pure white background, flat neutral lighting, no cast or floor shadow. No redesign, weapon, shield, text, scenery, base, or extra object.

## Character's Left Side

> Using the supplied approved concept/front image as strict identity reference, create the exact same character in a true left profile, showing the character's left side. [IDENTITY BLOCK]. Preserve [WORN ITEMS AND LOCATIONS] exactly; do not mirror or relocate them. Same relaxed A-pose, empty hands, full body centered, orthographic-like profile with no perspective distortion. Pure white background, flat neutral lighting, no cast or floor shadow. No redesign, weapon, shield, text, scenery, base, or extra object.

## Character's Right Side

> Using the supplied approved concept/front image as strict identity reference, create the exact same character in a true right profile, showing the character's right side. [IDENTITY BLOCK]. Preserve [WORN ITEMS AND LOCATIONS] exactly; do not mirror or relocate them. Same relaxed A-pose, empty hands, full body centered, orthographic-like profile with no perspective distortion. Pure white background, flat neutral lighting, no cast or floor shadow. No redesign, weapon, shield, text, scenery, base, or extra object.

## Separate Equipment Asset

> A single standalone [ITEM] belonging to the supplied approved character, matching the exact visual language, chunky simplified construction, palette, materials, wear, and serious-but-quirky tone of the reference. [FUNCTIONAL SHAPE AND REQUIRED COMPONENTS]. Practical and readable as a low-poly game asset, with restrained decoration and no fragile micro-detail. Entire item centered and unobstructed in [BEST ORIENTATION/VIEW], orthographic product presentation. Pure white background, flat neutral lighting, no cast or floor shadow. Item only: no character, hands, other equipment, stand, text, scenery, particles, or border.

Create a separate prompt for every item. Never ask the image generator to produce multiple assets in one image.

## Consistency Check

Before delivering prompts, verify:

- all four character views describe the same A-pose and framing;
- left and right mean the character's own left and right;
- worn attachments remain present and on the correct side;
- held assets are absent from character views and each has its own prompt;
- prompts require the approved concept as the strict reference;
- background and lighting instructions are identical;
- no prompt accidentally asks for a collage, turntable sheet, extra character, or redesign.
