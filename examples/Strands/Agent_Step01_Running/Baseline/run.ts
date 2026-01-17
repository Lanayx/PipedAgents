import { Agent } from '@strands-agents/sdk';
import { OpenAIModel } from '@strands-agents/sdk/openai';
import * as dotenv from 'dotenv';

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
        // name: "Joker" // Agent config doesn't seem to have a 'name' field based on d.ts, but instructions are passed via systemPrompt
    });

    // Run the agent
    const result = await agent.invoke("Tell me a joke about a pirate.");
    console.log(result.toString());
    console.log("Agent call completed.");
}

dotenv.config();
main().catch((err) => {
    console.error(err);
    process.exitCode = 1;
});