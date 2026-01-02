FROM docker.io/library/python:3.14.2-slim-bookworm@sha256:e8a1ad81a9fef9dc56372fb49b50818cac71f5fae238b21d7738d73ccae8f803
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
