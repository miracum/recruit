FROM docker.io/library/python:3.13.5-slim-bookworm@sha256:1020ca463dc51c26bbad49de85dbb2986d93b71050102f3fa2a7f0fc4c2ea81e
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
