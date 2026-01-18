import { Agent } from '@strands-agents/sdk';
import { OpenAIModel } from '@strands-agents/sdk/openai';

async function main() {
    // Initialize the OpenAI model
    const model = new OpenAIModel({
        modelId: process.env.MODEL_ID,
        apiKey: process.env.OPENAI_API_KEY,
        clientConfig: {
            baseURL: process.env.OPENAI_API_BASE_URL
        }
    });

    // Create the agent
    const agent = new Agent({
        model: model,
        systemPrompt: "You are good at telling jokes. Write jokes with all uppercase letters.",
        printer: false
    });

    // Run the agent
    console.log("Agent response stream:");
    for await (const event of agent.stream("Tell me a joke about a pirate.")) {
        if (event.type === 'modelContentBlockDeltaEvent' && event.delta.type === 'textDelta') {
            process.stdout.write(event.delta.text);
        }
    }
    console.log("\nAgent call completed.");
}

main().catch((err) => {
    console.error(err);
    process.exitCode = 1;
});