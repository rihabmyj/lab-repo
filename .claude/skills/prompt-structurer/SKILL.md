---
name: prompt-structurer
description: Converts a naive, vague, or one-line prompt into a well-engineered structured prompt using XML tags (<role>, <context>, <instructions>, <requirements>, <format_output>, <data_input>), applying advanced techniques like few-shot examples, chain-of-thought, and prompt chaining when they help. Asks the user for any missing critical information through a question window before writing. Use this skill whenever the user wants to improve, rewrite, structure, optimize, or "engineer" a prompt, says things like "make this prompt better", "structure my prompt", "turn this into a proper prompt", "améliore mon prompt", "prompt engineering", or pastes a short request and asks how to phrase it for an LLM — even if they don't mention tags or techniques.
---

# Prompt Structurer

Turn a naive prompt into a structured, production-quality prompt that any LLM can follow reliably. The output is **only the structured prompt** (plus a short note on techniques used) — do not execute the prompt yourself.

All output is written **in English**, even when the naive prompt is in another language (translate the intent faithfully; keep proper nouns, product names, and quoted user data in their original language).

## Workflow

### 1. Analyze the naive prompt

Read the naive prompt and extract, explicitly or by reasonable inference:

- **Task** — what the model must actually produce or do
- **Role** — what expert would do this best
- **Audience / purpose** — who will use the result and why
- **Context** — domain, product, background facts
- **Input data** — what material the model will work on (text, code, table, file…)
- **Output** — format, length, language, tone
- **Constraints** — must/must-not rules, quality bar

### 2. Detect missing information → open a question window

Some gaps can be filled with sensible defaults (tone, length, a generic expert role). Others change the prompt fundamentally — guessing them produces a prompt that looks good but solves the wrong problem. Ask about those.

Information is **critical** (ask) when:
- The task itself is ambiguous (e.g. "write about our product" — what product? what kind of text?)
- The target audience changes the content (beginners vs experts, customers vs developers)
- The output format is unclear and matters (email vs report vs JSON vs code)
- The input data is referenced but not provided or described ("analyze my data")
- A key business fact is needed that can't be inferred (company name, product features, platform)

When anything critical is missing, **use the `AskUserQuestion` tool** to open a question window before writing anything:
- Up to 4 questions per window, each standalone and specific
- Offer 2–4 concrete options per question, putting your best guess first with "(Recommended)" — the user can always pick "Other" to type their own answer
- Short `header` chips like "Audience", "Format", "Tone", "Data"
- If the answers reveal new critical gaps, open one more window; otherwise proceed

If `AskUserQuestion` isn't available in the environment, ask the same questions as a short numbered list in chat and wait for the answer.

Don't ask about things you can default well — record them as assumptions instead (see step 5). A window full of trivial questions is as bad as one with none.

### 3. Choose the techniques

Pick techniques based on what the task actually needs; using none beyond the basic structure is fine for simple tasks.

| Technique | Use when | How |
|---|---|---|
| **Few-shot** | Output style, format, or labeling must be precise and consistent (classification, extraction, product descriptions in a house style, rewriting) | Add an `<examples>` block with 2–3 `<example>` items, each with `<input>` and `<output>`. Make examples diverse (cover edge cases) so the model doesn't just copy one pattern. Write realistic examples, not placeholders. |
| **Chain-of-thought (CoT)** | Reasoning, analysis, math, diagnosis, decisions, comparing options, debugging | Ask the model to reason step by step inside `<thinking>` tags before giving the final answer inside `<answer>` tags. List the reasoning steps it should go through when the domain has a known method. |
| **Prompt chaining** | The task has distinct stages whose outputs feed each other (research → outline → draft → review; extract → analyze → report), or one prompt would be too long or mix very different skills | Split into several prompts `<prompt_1>`, `<prompt_2>`… Each is a full structured prompt. Pass results forward with placeholders like `{{output_of_prompt_1}}`. State the purpose of each step in one line above it. |

Techniques can combine (e.g. a chain whose analysis step uses CoT and whose writing step uses few-shot).

### 4. Write the structured prompt

Use this tag structure, in this order. Omit `<data_input>` only if the task genuinely has no input; omit technique tags when not used.

```xml
<role>
Who the model is: a specific expert with relevant experience — not just "a helpful assistant".
</role>

<context>
Background the model needs: domain, product/company, audience, why this task matters, what the result will be used for.
</context>

<instructions>
Numbered, imperative steps the model follows in order.
1. ...
2. ...
</instructions>

<requirements>
Concrete constraints and quality criteria: must-do, must-not-do, tone, length, language, accuracy rules, edge-case handling.
</requirements>

<examples>            <!-- few-shot only -->
<example>
<input>...</input>
<output>...</output>
</example>
</examples>

<format_output>
Exact shape of the answer: structure, headings, JSON schema, length, markdown or not. If CoT is used, specify <thinking> then <answer>.
</format_output>

<data_input>
{{placeholder_name}}  — or the actual data if the user provided it
</data_input>
```

Writing guidelines:
- **Be specific.** Replace vague words ("good", "nice", "detailed") with measurable criteria ("3–5 bullet points", "under 150 words", "include price and delivery time").
- **Explain the why** in `<context>` — a model that knows the purpose makes better judgment calls than one following bare rules.
- **Use `{{double_braces}}` placeholders** for any data the user will fill in later, with a descriptive name (`{{customer_review}}`, not `{{text}}`).
- **Keep instructions and requirements separate**: instructions = the steps; requirements = the rules every step must respect.
- **Tell the model what to do when input is bad** (empty, off-topic, ambiguous) if that can realistically happen.

### 5. Present the result

Reply with exactly these parts:

1. **The structured prompt** in a single ```xml code block (for chains: all prompts in one block, in order, each preceded by an XML comment stating its purpose), so the user can copy it in one go.
2. **Techniques used** — one line per technique and why it was chosen (or "Basic structure only — the task is simple enough").
3. **Assumptions** — defaults you chose without asking, so the user can correct them. Omit if none.

Keep the surrounding commentary short; the prompt is the deliverable.

## Example

**Naive prompt:** "write descriptions for my products"

**Question window** (critical gaps: which products, where they're shown, what style):
- "What kind of products are these?" → Fashion / Electronics / Home & decor / Food
- "Where will the descriptions appear?" → Product page of an online store (Recommended) / Marketplace like Amazon / Social media
- "What tone should they have?" → Friendly and persuasive (Recommended) / Premium and elegant / Technical and factual

Suppose the user answers: Electronics, online store product page, friendly and persuasive.

**Output:**

```xml
<role>
You are a senior e-commerce copywriter specialized in consumer electronics, with experience writing product pages that convert visitors into buyers.
</role>

<context>
The descriptions will appear on product pages of an online electronics store. Visitors are everyday consumers, not technicians: they want to quickly understand what the product does for them and whether it fits their needs. A clear, persuasive description reduces hesitation and returns.
</context>

<instructions>
1. Read the product information in <data_input>.
2. Identify the 3 most important benefits for an everyday user (translate technical specs into concrete benefits).
3. Write a catchy one-line headline.
4. Write a short paragraph presenting the product and its main benefit.
5. List the key features as bullet points, each pairing a spec with its benefit.
6. End with a one-sentence call to action.
</instructions>

<requirements>
- Friendly, persuasive tone; address the reader as "you".
- 80–120 words total, excluding the headline.
- Only use facts present in the product information; never invent specs, certifications, or prices.
- No jargon without a plain-language explanation.
- If key information is missing (e.g. no specs given), write the description with what is available and list the missing fields after the description under "Missing info:".
</requirements>

<examples>
<example>
<input>Name: SoundPod Mini. Type: Bluetooth speaker. Battery: 12h. Waterproof: IPX7. Weight: 300 g.</input>
<output>
**Your music, anywhere — even in the rain.**
The SoundPod Mini fits in your bag and keeps the party going all day long.
- **12-hour battery** — from morning coffee to late-night playlists on one charge
- **IPX7 waterproof** — pool, beach, or shower: splashes are no problem
- **Only 300 g** — light enough to take everywhere
Grab yours and bring your soundtrack wherever you go.
</output>
</example>
<example>
<input>Name: ChargeFlex 3-in-1. Type: wireless charger. Devices: phone, earbuds, watch. Power: 15 W.</input>
<output>
**One spot. Every device. Zero cable mess.**
ChargeFlex 3-in-1 powers up your phone, earbuds, and watch at the same time, so your nightstand stays tidy.
- **Charges 3 devices at once** — no more fighting over the outlet
- **15 W fast charging** — your phone is ready before you are
- **Cable-free design** — just drop your devices and go
Simplify your evenings — add ChargeFlex to your cart today.
</output>
</example>
</examples>

<format_output>
Markdown, in this order: bold headline, one short paragraph, 3–5 bullet points with bold feature names, one call-to-action sentence. No extra commentary before or after.
</format_output>

<data_input>
{{product_information}}
</data_input>
```

**Techniques used:**
- Few-shot (2 examples) — locks in a consistent structure and tone across many products.

**Assumptions:**
- Length of 80–120 words, suited to a product page.
