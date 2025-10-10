FROM docker.io/library/python:3.14.0-slim-bookworm@sha256:f241b2f9bf06e07f4018c9f84d4910c0bc3066a42945ddedc715afa6867e5551
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
