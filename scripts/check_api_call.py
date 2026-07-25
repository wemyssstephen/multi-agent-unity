from multi_agent_unity.client import build_request_payload, print_response_to_terminal, send_request

if __name__ == "__main__":
    headers, body = build_request_payload("Hello, Claude!")
    response = send_request(headers, body)
    print_response_to_terminal(response)
