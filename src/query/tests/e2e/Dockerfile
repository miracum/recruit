FROM docker.io/library/python:3.13.3-slim-bookworm@sha256:60248ff36cf701fcb6729c085a879d81e4603f7f507345742dc82d4b38d16784
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
