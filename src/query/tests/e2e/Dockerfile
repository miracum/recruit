FROM docker.io/library/python:3.14.3-slim-bookworm@sha256:f0540d0436a220db0a576ccfe75631ab072391e43a24b88972ef9833f699095f
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
