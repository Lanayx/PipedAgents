/**
 * Creates a custom fetch function that logs requests and responses.
 * 
 * @param logFn Optional custom logging function (defaults to console.log)
 * @returns A fetch-compatible function
 */
export async function loggingFetch(input: any, init?: any): Promise<Response> {
    const method = init?.method || 'GET';
    const url = input.toString();

    console.log(`\n[LoggingFetch] Request: ${method} ${url}`);

    if (init?.body) {
        console.log(`[LoggingFetch] Body: ${init.body}`);
    }

    try {
        const response = await global.fetch(input, init);
        console.log(`[LoggingFetch] Response Status: ${response.status}`);

        const clone = response.clone();
        clone.text().then(text => {
            console.log(`[LoggingFetch] Response Body: ${text}`);
        }).catch(err => {
            console.log(`[LoggingFetch] Error reading response body: ${err}`);
        });

        return response;
    } catch (error) {
        console.log(`[LoggingFetch] Error: ${error}`);
        throw error;
    }
}
