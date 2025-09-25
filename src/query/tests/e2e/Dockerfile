FROM docker.io/library/python:3.13.7-slim-bookworm@sha256:5fa2567616c8d0e9a9470c8b4c1cb8b6f4d9f2fd45a548df393bed3537a7a324
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
