FROM docker.io/library/python:3.13.0-slim-bookworm@sha256:4efa69bf17cfbd83a9942e60e2642335c3b397448e00410063a0421f9727c4c4
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
