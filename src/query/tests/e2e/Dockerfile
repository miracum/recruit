FROM docker.io/library/python:3.13.7-slim-bookworm@sha256:4e368beeae9bf2720518bf8f45a5ecb701adadfcbb01142561fd7a4c99989a86
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
